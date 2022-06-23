using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JavScraper.Configuration;
using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Extensions;
using Jellyfin.Plugin.JavScraper.Scrapers;
using Jellyfin.Plugin.JavScraper.Scrapers.Model;
using Jellyfin.Plugin.JavScraper.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Providers
{
    public class JavMovieProvider : IRemoteMetadataProvider<Movie, MovieInfo>, IHasOrder
    {
        /// <summary>
        /// 番号最低满足条件：字母、数字、横杠、下划线
        /// </summary>
        private static readonly Regex _regexNum = new("^[-_ a-z0-9]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private readonly ILogger _logger;
        private readonly TranslationService _translationService;
        private readonly ImageProxyService _imageProxyService;
        private readonly IApplicationPaths _appPaths;
        private readonly GfriendsAvatarService _gfriendsAvatarService;
        private readonly ApplicationDatabase _applicationDatabase;
        private readonly Dictionary<string, IScraper> _scrapers;

        public JavMovieProvider(
            ILoggerFactory loggerFactory,
            IApplicationPaths appPaths,
            ImageProxyService imageProxyService,
            GfriendsAvatarService gfriendsAvatarService,
            TranslationService translationService,
            ApplicationDatabase applicationDatabase,
            IEnumerable<IScraper> scrapers)
        {
            _logger = loggerFactory.CreateLogger<JavMovieProvider>();
            _translationService = translationService;
            _imageProxyService = imageProxyService;
            _gfriendsAvatarService = gfriendsAvatarService;
            _applicationDatabase = applicationDatabase;
            _appPaths = appPaths;
            _scrapers = scrapers.ToDictionary(x => x.Name);
        }

        public int Order => 4;

        public string Name => "JavScraper";

        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
            => _imageProxyService.GetImageResponse(url, ImageType.Backdrop, cancellationToken);

        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            _logger.LogInformation("call {Method} {Args}", nameof(GetMetadata), $"{nameof(info)}={info.ToJson()}");
            var metadataResult = new MetadataResult<Movie>();
            var index = info.GetJavVideoIndex();

            if (index == null)
            {
                _logger.LogInformation("Could not find index from info={Info}, try to search!", info.ToJson());
                var res = await SearchByName(info.Name).ConfigureAwait(false);
                index = res.FirstOrDefault();
            }

            if (index == null)
            {
                _logger.LogInformation("Could not find index for info={Info}, exit!", info);
                return metadataResult;
            }

            var vedio = await _scrapers[index.Provider].GetJavVideo(index).ConfigureAwait(false);
            if (vedio == null)
            {
                _logger.LogInformation("Could not retrieve detail for index={Index}, try to retrieve from database.", index);
                _applicationDatabase.FindJavVideo(index.Provider, index.Url);
            }
            else
            {
                _applicationDatabase.SaveJavVideo(vedio);
            }

            if (vedio == null)
            {
                _logger.LogInformation("Could not retrieve detail for index={Index}, exit!", index);
                return metadataResult;
            }

            _logger.LogInformation("vedio found for name={Name}, result={Vedio}", info.Name, vedio);

            metadataResult.HasMetadata = true;
            metadataResult.QueriedById = true;

            // 忽略部分类别
            if (vedio.Genres.Any())
            {
                vedio.Genres = vedio.Genres
                    .Where(o => !Plugin.Instance.Configuration.IsIgnoreGenre(o))
                    .Where(o => !(Plugin.Instance.Configuration.GenreIgnoreActor && vedio.Actors.Contains(o)))
                    .ToList();
            }

            // 从标题结尾处移除女优的名字
            if (Plugin.Instance.Configuration.GenreIgnoreActor && vedio.Actors.Any() && !string.IsNullOrWhiteSpace(vedio.Title))
            {
                var title = vedio.Title.Trim();
                var found = false;
                do
                {
                    found = false;
                    foreach (var actor in vedio.Actors)
                    {
                        if (title.EndsWith(actor, StringComparison.OrdinalIgnoreCase))
                        {
                            title = title[..^actor.Length].TrimEnd().TrimEnd(",， ".ToArray()).TrimEnd();
                            found = true;
                        }
                    }
                }
                while (found);
                vedio.Title = title;
            }

            vedio.OriginalTitle = vedio.Title;

            // 替换标签
            var genreReplaceMaps = Plugin.Instance.Configuration.EnableGenreReplace ? Plugin.Instance.Configuration.GenreReplaceMaps : Array.Empty<(string, string)>();
            if (genreReplaceMaps.Any() && vedio.Genres.Any())
            {
                var q =
                    from c in vedio.Genres
                    join p in genreReplaceMaps on c equals p.Source into ps
                    from p in ps.DefaultIfEmpty()
                    select p.Target ?? c;
                vedio.Genres = q.Where(o => !o.Contains("XXX", StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // 替换演员姓名
            var actorReplaceMaps = Plugin.Instance.Configuration.EnableActorReplace ? Plugin.Instance.Configuration.ActorReplaceMaps : null;
            if (actorReplaceMaps?.Any() == true && vedio.Actors.Any())
            {
                var q =
                    from c in vedio.Actors
                    join p in actorReplaceMaps on c equals p.Source into ps
                    from p in ps.DefaultIfEmpty()
                    select p.Target ?? c;
                vedio.Actors = q.Where(o => !o.Contains("XXX", StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // 翻译
            if (Plugin.Instance.Configuration.EnableBaiduFanyi)
            {
                var op = (BaiduFanyiOptions)Plugin.Instance.Configuration.BaiduFanyiOptions;
                if (genreReplaceMaps != null && genreReplaceMaps.Any() && op.HasFlag(BaiduFanyiOptions.Genre))
                {
                    op &= ~BaiduFanyiOptions.Genre;
                }

                var lang = Plugin.Instance.Configuration.BaiduFanyiLanguage?.Trim();
                if (string.IsNullOrWhiteSpace(lang))
                {
                    lang = "zh";
                }

                if (op.HasFlag(BaiduFanyiOptions.Name))
                {
                    vedio.Title = await _translationService.Translate(vedio.Title).ConfigureAwait(false);
                }

                if (op.HasFlag(BaiduFanyiOptions.Plot))
                {
                    vedio.Overview = await _translationService.Translate(vedio.Overview).ConfigureAwait(false);
                }

                if (op.HasFlag(BaiduFanyiOptions.Genre))
                {
                    vedio.Genres = await _translationService.Translate(vedio.Genres).ConfigureAwait(false);
                }
            }

            var cc = new[] { "-C", "-C2", "_C", "_C2" };
            if (Plugin.Instance.Configuration.AddChineseSubtitleGenre && cc.Any(v => info.Name.EndsWith(v, StringComparison.OrdinalIgnoreCase)))
            {
                const string CHINESE_SUBTITLE_GENRE = "中文字幕";
                if (vedio.Genres == null)
                {
                    vedio.Genres = new List<string>() { CHINESE_SUBTITLE_GENRE };
                }
                else if (!vedio.Genres.Contains(CHINESE_SUBTITLE_GENRE))
                {
                    vedio.Genres = vedio.Genres.Append("中文字幕").ToList();
                }
            }

            // 格式化标题
            var name = $"{vedio.Num} {vedio.Title}";
            if (!string.IsNullOrWhiteSpace(Plugin.Instance.Configuration.TitleFormat))
            {
                name = vedio.GetFormatName(Plugin.Instance.Configuration.TitleFormat, Plugin.Instance.Configuration.TitleFormatEmptyValue);
            }

            metadataResult.Item = new Movie
            {
                OfficialRating = "XXX",
                Name = name,
                Overview = vedio.Overview,
                ProductionYear = vedio.GetYear(),
                OriginalTitle = vedio.OriginalTitle,
                Genres = vedio.Genres.ToArray(),
                SortName = vedio.Num,
                ForcedSortName = vedio.Num,
                ExternalId = vedio.Num
            };

            if (vedio.CommunityRating >= 0 && vedio.CommunityRating <= 10)
            {
                metadataResult.Item.CommunityRating = vedio.CommunityRating;
            }

            metadataResult.Item.CollectionName = vedio.Set;
            if (vedio.Genres?.Any() == true)
            {
                foreach (var genre in vedio.Genres.Where(o => !string.IsNullOrWhiteSpace(o)).Distinct())
                {
                    metadataResult.Item.AddGenre(genre);
                }
            }

            metadataResult.Item.SetJavVideoIndex(vedio);

            var dt = vedio.GetDateTime();
            if (dt != null)
            {
                metadataResult.Item.PremiereDate = metadataResult.Item.DateCreated = dt.Value;
            }

            if (!string.IsNullOrWhiteSpace(vedio.Studio))
            {
                metadataResult.Item.AddStudio(vedio.Studio);
            }

            var cut_persion_image = Plugin.Instance.Configuration.EnableCutPersonImage;
            var person_image_type = cut_persion_image ? ImageType.Primary : ImageType.Backdrop;

            // 添加人员
            async Task AddPerson(string personName, string personType)
            {
                var person = new PersonInfo
                {
                    Name = personName,
                    Type = personType,
                };
                var url = await _gfriendsAvatarService.FindAvatarAddressAsync(person.Name, cancellationToken).ConfigureAwait(false);
                if (url.IsWebUrl())
                {
                    person.ImageUrl = _imageProxyService.GetLocalUrl(url, person_image_type);
                }

                metadataResult.AddPerson(person);
            }

            if (!string.IsNullOrWhiteSpace(vedio.Director))
            {
                await AddPerson(vedio.Director, PersonType.Director).ConfigureAwait(false);
            }

            if (vedio.Actors.Any())
            {
                foreach (var actor in vedio.Actors)
                {
                    await AddPerson(actor, PersonType.Actor).ConfigureAwait(false);
                }
            }

            return metadataResult;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
        {
            _logger.LogInformation("call {Method} {Args}", nameof(GetSearchResults), $"{nameof(searchInfo)}={searchInfo.ToJson()}");
            var indexList = await SearchByName(searchInfo.Name).ConfigureAwait(false);

            return indexList.Select(vedioIndex =>
            {
                var result = new RemoteSearchResult
                {
                    Name = $"{vedioIndex.Num} {vedioIndex.Title}",
                    ProductionYear = vedioIndex.GetYear(),
                    ImageUrl = _imageProxyService.GetLocalUrl(vedioIndex.Cover, withApiUrl: false),
                    SearchProviderName = Name,
                    PremiereDate = vedioIndex.GetDateTime(),
                };
                result.SetJavVideoIndex(vedioIndex);
                return result;
            })
            .ToArray();
        }

        private async Task<IEnumerable<JavVideoIndex>> SearchByName(string name)
        {
            _logger.LogInformation("call {Method} {Args}", nameof(SearchByName), $"{nameof(name)}={name}");
            if (string.IsNullOrWhiteSpace(name))
            {
                return Enumerable.Empty<JavVideoIndex>();
            }

            var javid = JavIdRecognizer.Parse(name);
            if (javid == null && !_regexNum.IsMatch(name))
            {
                return Enumerable.Empty<JavVideoIndex>();
            }

            var key = javid?.Id ?? name;
            var scraperConfigMap = Plugin.Instance.Configuration.ScraperConfigList.ToDictionary(scraperConfig => scraperConfig.Name);
            var scrapers = _scrapers.Values.Where(scraper => scraperConfigMap[scraper.Name]?.Enable != false).ToList();

            var results = await Task.WhenAll(scrapers.Select(scraper => scraper.Search(key)).ToArray()).ConfigureAwait(false);

            var indexList = results.SelectMany(result => result).ToList();
            _logger.LogInformation("There are {N} indexs found for key={Key}", indexList.Count, key);

            // order by scraper priority
            return scrapers
                .Join(
                    indexList.GroupBy(vedioIndex => vedioIndex.Provider),
                    scraper => scraper.Name,
                    vedioIndexGroup => vedioIndexGroup.Key,
                    (scraper, vedioIndexGroup) => vedioIndexGroup)
                .SelectMany(vedioIndexGroup => vedioIndexGroup)
                .ToList();
        }
    }
}
