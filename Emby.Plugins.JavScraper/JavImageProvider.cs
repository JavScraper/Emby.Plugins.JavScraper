using Emby.Plugins.JavScraper.Scrapers;
using Emby.Plugins.JavScraper.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;

#if __JELLYFIN__

using Microsoft.Extensions.Logging;

#else
using MediaBrowser.Model.Logging;
#endif

using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using System;
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

        public JavImageProvider(IHttpClient httpClient, IProviderManager providerManager, ILogger logger, IJsonSerializer jsonSerializer, IFileSystem fileSystem, IApplicationPaths appPaths)
        {
            _httpClient = httpClient;
            this.providerManager = providerManager;
            _logger = logger;
            _appPaths = appPaths;
            _jsonSerializer = jsonSerializer;
            ImageProxyService = new ImageProxyService(jsonSerializer, logger, fileSystem, appPaths);
        }

        public string Name => Plugin.NAME;

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            _logger?.Info($"{Name}-{nameof(JavImageProvider)}-{nameof(GetImageResponse)} {url}");
            return ImageProxyService.GetImageResponse(url, cancellationToken);
        }

#if __JELLYFIN__

        public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
            => GetImages(item, null, cancellationToken);

#endif

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

            if (string.IsNullOrWhiteSpace(m.Cover) && m.Samples?.Any() == true)
                m.Cover = m.Samples.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(m.Cover) == false)
            {
                async Task SaveImage(ImageType type)
                {
                    //有的就跳过了
                    if (item.ImageInfos?.Any(o => o.Type == type) == true)
                        return;
                    try
                    {
                        var url = ImageProxyService.BuildUrl(m.Cover, type == ImageType.Primary ? 1 : 0);
                        var resp = await ImageProxyService.GetImageResponse(url, cancellationToken);
                        if (resp?.ContentLength > 0)
                        {
#if __JELLYFIN__
                            await providerManager.SaveImage(item, resp.Content, resp.ContentType, type, 0, cancellationToken);
#else
                            await providerManager.SaveImage(item, libraryOptions, resp.Content, resp.ContentType.ToArray(), type, 0, cancellationToken);
#endif
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Warn($"Save image error: {type} {m.Cover} {ex.Message}");
                    }
                }

                await SaveImage(ImageType.Primary);
                await SaveImage(ImageType.Backdrop);
                //await SaveImage(ImageType.Art);

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