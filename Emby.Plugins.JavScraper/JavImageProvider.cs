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
            ImageProxyService = new ImageProxyService(logger);
        }

        public string Name => Plugin.NAME;

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            _logger?.Info($"{Name}-{nameof(JavImageProvider)}-{nameof(GetImageResponse)} {url}");
            return ImageProxyService.GetImageResponse(url, cancellationToken);
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions, CancellationToken cancellationToken)
        {
            _logger?.Info($"{Name}-{nameof(GetImages)} name:{item.Name}");

            var list = new List<RemoteImageInfo>();
            JavVideoIndex index = null;
            if (!item.ProviderIds.TryGetValue($"{Name}-Json", out string json) || (index = _jsonSerializer.DeserializeFromString<JavVideoIndex>(json)) == null)
            {
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
            }

            if (m == null)
            {
                //var a = new RemoteImageInfo()
                //{
                //    ProviderName = Name,
                //    Type = ImageType.Primary,
                //    Url = ImageProxyService.BuildUrl(index.Cover, 1),
                //};
                //list.Add(a);

                return list;
            }

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

                //var a = new RemoteImageInfo()
                //{
                //    ProviderName = Name,
                //    Type = ImageType.Primary,
                //    Url = ImageProxyService.BuildUrl(m.Cover, 1),
                //};
                //list.Add(a);

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
                    Type = ImageType.Screenshot,
                    Url = Plugin.Instance.Configuration.BuildProxyUrl(o),
                }));
            }

            return list;
        }

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
            => new[] { ImageType.Primary, ImageType.Backdrop, ImageType.Screenshot };

        public bool Supports(BaseItem item) => item is Movie;
    }
}