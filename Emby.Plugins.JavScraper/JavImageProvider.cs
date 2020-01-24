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
    public class JavImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClient _httpClient;
        private readonly IProviderManager providerManager;
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IApplicationPaths _appPaths;
        public ImageProxyService ImageProxyService { get; }

        public JavImageProvider(IHttpClient httpClient, IProviderManager providerManager, ILogger logger, IJsonSerializer jsonSerializer, IApplicationPaths appPaths)
        {
            _httpClient = httpClient;
            this.providerManager = providerManager;
            _logger = logger;
            _appPaths = appPaths;
            _jsonSerializer = jsonSerializer;
            ImageProxyService = new ImageProxyService(jsonSerializer, logger);
        }

        public string Name => Plugin.NAME;

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            _logger?.Info($"{Name}-{nameof(JavImageProvider)}-{nameof(GetImageResponse)} {url}");
            return ImageProxyService.GetImageResponse(url, cancellationToken);
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions, CancellationToken cancellationToken)
        {
            _logger?.Info($"{Name}-{nameof(JavImageProvider)}-{nameof(GetImages)} name:{item.Name}");

            var list = new List<RemoteImageInfo>();
            JavVideoIndex index = null;
            if ((index = item.GetJavVideoIndex(_jsonSerializer)) == null)
            {
                _logger?.Info($"{Name}-{nameof(JavImageProvider)}-{nameof(GetImages)} name:{item.Name} JavVideoIndex not found.");
                return list;
            }

            JavVideo m = null;
            try
            {
                var cachePath = Path.Combine(_appPaths.CachePath, Name, index.Provider, $"{index.Num}.json");
                m = _jsonSerializer.DeserializeFromFile<JavVideo>(cachePath);
            }
            catch
            {
                _logger?.Info($"{Name}-{nameof(JavImageProvider)}-{nameof(GetImages)} name:{item.Name} JavVideo not found.");
            }

            if (m == null)
                return list;

            if (string.IsNullOrWhiteSpace(m.Cover) == false)
            {
                if (string.IsNullOrWhiteSpace(m.Cover) == false && item.ImageInfos?.Any(o => o.Type == ImageType.Primary) != true)
                {
                    try
                    {
                        var url = ImageProxyService.BuildUrl(m.Cover, 1);
                        var resp = await ImageProxyService.GetImageResponse(url, cancellationToken);
                        if (resp.ContentLength > 0)
                        {
                            try
                            {
                                await providerManager.SaveImage(item, libraryOptions, resp.Content, resp.ContentType.ToArray(), ImageType.Primary, null, cancellationToken);
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                var b = new RemoteImageInfo()
                {
                    ProviderName = Name,
                    Type = ImageType.Backdrop,
                    Url = Plugin.Instance.Configuration.BuildProxyUrl(m.Cover),
                };
                list.Add(b);
            }

            if (m.Samples?.Any() == true)
            {
                list.AddRange(m.Samples.Select(o => new RemoteImageInfo()
                {
                    ProviderName = Name,
                    Type = ImageType.Art,
                    Url = Plugin.Instance.Configuration.BuildProxyUrl(o),
                }));
            }

            return list;
        }

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
            => new[] { ImageType.Primary, ImageType.Backdrop, ImageType.Art };

        public bool Supports(BaseItem item) => item is Movie;
    }
}