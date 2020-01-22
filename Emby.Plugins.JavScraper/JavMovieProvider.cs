using Emby.Plugins.JavScraper.Scrapers;
using Emby.Plugins.JavScraper.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper
{
    public class JavMovieProvider : IRemoteMetadataProvider<Movie, MovieInfo>, IHasOrder
    {
        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly IProviderManager providerManager;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IApplicationPaths _appPaths;

        private List<AbstractScraper> scrapers;
        public ImageProxyService ImageProxyService { get; }

        public JavMovieProvider(ILogger logger, IProviderManager providerManager, IHttpClient httpClient, IJsonSerializer jsonSerializer, IApplicationPaths appPaths)
        {
            _logger = logger;
            this.providerManager = providerManager;
            _httpClient = httpClient;
            _jsonSerializer = jsonSerializer;
            _appPaths = appPaths;
            scrapers = new List<AbstractScraper>() { new JavBus(null, logger), new JavDB(null, logger), new MgsTage(null, logger) };
            ImageProxyService = new ImageProxyService(logger);
        }

        public int Order => 4;

        public string Name => Plugin.NAME;

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            _logger?.Info($"{Name}-{nameof(JavMovieProvider)}-{nameof(GetImageResponse)} {url}");
            return ImageProxyService.GetImageResponse(url, cancellationToken);
        }

        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            var metadataResult = new MetadataResult<Movie>();
            JavVideoIndex index = null;

            _logger?.Info($"{Name}-{nameof(GetMetadata)} name:{info.Name}");

            if (!info.ProviderIds.TryGetValue($"{Name}-Json", out string json) || (index = _jsonSerializer.DeserializeFromString<JavVideoIndex>(json)) == null)
            {
                var res = await GetSearchResults(info, cancellationToken).ConfigureAwait(false);
                if (res.Count() == 0 || !res.FirstOrDefault().ProviderIds.TryGetValue($"{Name}-Json", out json) || (index = _jsonSerializer.DeserializeFromString<JavVideoIndex>(json)) == null)
                {
                    _logger?.Info($"{Name}-{nameof(GetMetadata)} name:{info.Name} not found 0.");
                    return metadataResult;
                }
            }

            if (index == null)
            {
                _logger?.Info($"{Name}-{nameof(GetMetadata)} name:{info.Name} not found 1.");
                return metadataResult;
            }

            var sc = scrapers.FirstOrDefault(o => o.Name == index.Provider);
            if (sc == null)
                return metadataResult;

            var m = await sc.Get(index);

            if (m == null)
            {
                _logger?.Info($"{Name}-{nameof(GetMetadata)} name:{info.Name} not found 2.");
                return metadataResult;
            }

            _logger?.Info($"{Name}-{nameof(GetMetadata)} name:{info.Name} {_jsonSerializer.SerializeToString(m)}");

            metadataResult.HasMetadata = true;
            metadataResult.QueriedById = true;

            metadataResult.Item = new Movie
            {
                Name = $"{m.Num} {m.Title}",
                Overview = m.Plot,
                ProductionYear = m.GetYear(),
                OriginalTitle = m.Title,
                Genres = m.Genres?.ToArray(),
                CollectionName = m.Set,
                SortName = m.Num,
                ExternalId = m.Num,
            };

            var dt = m.GetDate();
            if (dt != null)
                metadataResult.Item.PremiereDate = metadataResult.Item.DateCreated = dt.Value;

            if (!string.IsNullOrWhiteSpace(m.Studio))
                metadataResult.Item.AddStudio(m.Studio);

            if (!string.IsNullOrWhiteSpace(m.Director))
            {
                var pi = new PersonInfo
                {
                    Name = m.Director,
                    Type = PersonType.Director,
                };
                metadataResult.AddPerson(pi);
            }

            metadataResult.Item.ProviderIds.Add(Name, m.Num);

            if (m.Actors?.Any() == true)
                foreach (var cast in m.Actors)
                {
                    var pi = new PersonInfo
                    {
                        Name = cast,
                        Type = PersonType.Actor,
                    };
                    metadataResult.AddPerson(pi);
                }


            //var imgs = new List<ItemImageInfo>();
            //if (string.IsNullOrWhiteSpace(m.Cover) == false)
            //{
            //    var a = new ItemImageInfo()
            //    {
            //        DateModified = metadataResult.Item.DateModified,
            //        Type = MediaBrowser.Model.Entities.ImageType.Primary,
            //        Path = ImageProxyService.BuildUrl(m.Cover, 1),
            //    };
            //    imgs.Add(a);

            //    var b = new ItemImageInfo()
            //    {
            //        DateModified = metadataResult.Item.DateModified,
            //        Type = MediaBrowser.Model.Entities.ImageType.Backdrop,
            //        Path = ImageProxyService.BuildUrl(m.Cover, 0),
            //    };
            //    imgs.Add(b);
            //}

            //if (m.Samples?.Any() == true)
            //{
            //    imgs.AddRange(m.Samples.Select(o => new ItemImageInfo()
            //    {
            //        DateModified = metadataResult.Item.DateModified,
            //        Type = MediaBrowser.Model.Entities.ImageType.Screenshot,
            //        Path = ImageProxyService.BuildUrl(o, 0),
            //    }));
            //}

            //if (imgs.Any())
            //{
            //    if (metadataResult.Item.ImageInfos?.Any() != true)
            //        metadataResult.Item.ImageInfos = imgs.ToArray();
            //    else
            //        metadataResult.Item.ImageInfos = metadataResult.Item.ImageInfos.Union(imgs).ToArray();
            //}

            try
            {
                var cachePath = Path.Combine(_appPaths.CachePath, Name, m.Provider, $"{m.Num}.json");
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                _jsonSerializer.SerializeToFile(m, cachePath);
            }
            catch
            {
            }

            return metadataResult;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
        {
            var list = new List<RemoteSearchResult>();
            if (string.IsNullOrWhiteSpace(searchInfo.Name))
                return list;

            var javid = JavIdRecognizer.Parse(searchInfo.Name);
            if (javid == null && searchInfo.Name?.Length > 12)
                return list;
            var key = javid?.id ?? searchInfo.Name;

            _logger?.Info($"{Name}-{nameof(GetSearchResults)} name:{searchInfo.Name} id:{javid?.id}");

            var tasks = scrapers.Select(o => o.Query(key)).ToArray();
            await Task.WhenAll(tasks);
            var all = tasks.Where(o => o.Result?.Any() == true).SelectMany(o => o.Result).ToList();

            _logger?.Info($"{Name}-{nameof(GetSearchResults)} name:{searchInfo.Name} id:{javid?.id} count:{all.Count}");

            if (all.Any() != true)
                return list;

            foreach (var m in all)
            {
                var result = new RemoteSearchResult
                {
                    Name = $"{m.Num} {m.Title}",
                    ProductionYear = m.GetYear(),
                    ImageUrl = Plugin.Instance.Configuration.BuildProxyUrl(m.Cover),
                    SearchProviderName = Name,
                    PremiereDate = m.GetDate(),
                };
                result.ProviderIds.Add(Name, m.Num);
                result.ProviderIds.Add($"{Name}-Json", _jsonSerializer.SerializeToString(m));
                result.ProviderIds.Add($"{Name}-Url", m.Url);
                list.Add(result);
            }
            return list;
        }
    }
}