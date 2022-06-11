using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JavScraper.Configuration;
using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Extensions;
using Jellyfin.Plugin.JavScraper.Scrapers.Model;
using Jellyfin.Plugin.JavScraper.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
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
        private readonly ApplicationDbContext _applicationDbContext;

        public JavMovieProvider(
            ILoggerFactory loggerFactory,
            IApplicationPaths appPaths,
            ImageProxyService imageProxyService,
            GfriendsAvatarService gfriendsAvatarService,
            TranslationService translationService,
            ApplicationDbContext applicationDbContext)
        {
            _logger = loggerFactory.CreateLogger<JavMovieProvider>();
            _translationService = translationService;
            _imageProxyService = imageProxyService;
            _gfriendsAvatarService = gfriendsAvatarService;
            _applicationDbContext = applicationDbContext;
            _appPaths = appPaths;
        }

        public int Order => 4;

        public string Name => "JavScraper";

        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
            => _imageProxyService.GetImageResponse(url, ImageType.Backdrop, cancellationToken);

        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            _logger.LogInformation("{Method} info:{Input}", nameof(GetMetadata), JsonSerializer.Serialize(info));
            var metadataResult = new MetadataResult<Movie>();
            JavVideoIndex? index = null;

            if ((index = info.GetJavVideoIndex()) == null)
            {
                var res = await GetSearchResults(info, cancellationToken).ConfigureAwait(false);
                if (!res.Any() || (index = res.FirstOrDefault()?.GetJavVideoIndex()) == null)
                {
                    _logger.LogInformation("{Method} name:{Name} not found 0.", nameof(GetMetadata), info.Name);
                    return metadataResult;
                }
            }

            if (index == null)
            {
                _logger.LogInformation("{Method} name:{Name} not found 1.", nameof(GetMetadata), info.Name);
                return metadataResult;
            }

            var sc = Plugin.Instance.Scrapers.FirstOrDefault(o => o.Name == index.Provider);
            if (sc == null)
            {
                return metadataResult;
            }

            var vedio = await sc.GetJavVedio(index).ConfigureAwait(false);
            if (vedio != null)
            {
                _applicationDbContext.SaveJavVideo(vedio);
            }
            else
            {
                _applicationDbContext.FindJavVideo(index.Provider, index.Url);
            }

            if (vedio == null)
            {
                _logger.LogInformation("{Method} name:{Name} not found 2.", nameof(GetMetadata), info.Name);
                return metadataResult;
            }

            _logger.LogInformation("{Method} name:{Name} {Result}", nameof(GetMetadata), info.Name, JsonSerializer.Serialize(vedio));

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
            var genreReplaceMaps = Plugin.Instance.Configuration.EnableGenreReplace ? Plugin.Instance.Configuration.GenreReplaceMaps : null;
            if (genreReplaceMaps != null && genreReplaceMaps.Any() == true && vedio.Genres.Any())
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
                var arr = new List<string>();
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
            if (string.IsNullOrWhiteSpace(Plugin.Instance?.Configuration?.TitleFormat) == false)
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
                Genres = vedio.Genres?.ToArray() ?? Array.Empty<string>(),
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

            var cut_persion_image = Plugin.Instance?.Configuration?.EnableCutPersonImage ?? true;
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
            var list = new List<RemoteSearchResult>();
            if (string.IsNullOrWhiteSpace(searchInfo.Name))
            {
                return list;
            }

            var javid = JavIdRecognizer.Parse(searchInfo.Name);

            _logger.LogInformation("{Method} id:{ID} info:{SearchInfo}", nameof(GetSearchResults), javid?.Id, JsonSerializer.Serialize(searchInfo));

            // 自动搜索的时候，Name=文件夹名称，有时候是不对的，需要跳过
            if (javid == null && (searchInfo.Name.Length > 12 || !_regexNum.IsMatch(searchInfo.Name)))
            {
                return list;
            }

            var key = javid?.Id ?? searchInfo.Name;
            var scrapers = Plugin.Instance.Scrapers.Where(scraper => Plugin.Instance.Configuration.ScraperConfigList.GetConfigByName(scraper.Name)?.Enable != false).ToList();

            var results = await Task.WhenAll(scrapers.Select(scraper => scraper.Query(key)).ToArray()).ConfigureAwait(false);
            var vedioIndexList = results.SelectMany(result => result).ToList();

            _logger.LogInformation("{Method} name:{Name} id:{Id} count:{Count}", nameof(GetSearchResults), searchInfo.Name, javid?.Id, vedioIndexList.Count);

            if (!vedioIndexList.Any())
            {
                return list;
            }

            vedioIndexList = scrapers
                  .Join(
                      vedioIndexList.GroupBy(vedioIndex => vedioIndex.Provider),
                      scraper => scraper.Name,
                      vedioIndexGroup => vedioIndexGroup.Key,
                      (scraper, vedioIndexGroup) => vedioIndexGroup)
                  .SelectMany(vedioIndexGroup => vedioIndexGroup)
                  .ToList();

            return vedioIndexList.Select(vedioIndex =>
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
            });
        }
    }
}
