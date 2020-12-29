using Emby.Plugins.JavScraper.Scrapers;
using Emby.Plugins.JavScraper.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;

#if __JELLYFIN__
using Microsoft.Extensions.Logging;
#else
using MediaBrowser.Model.Logging;
#endif

using MediaBrowser.Model.Serialization;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.MediaInfo;

namespace Emby.Plugins.JavScraper
{
    public class JavImageProvider : IDynamicImageProvider
    {
        private readonly IHttpClient _httpClient;
        private readonly IProviderManager providerManager;
        private readonly ILibraryManager libraryManager;
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IApplicationPaths _appPaths;
        public ImageProxyService ImageProxyService => Plugin.Instance.ImageProxyService;

        public JavImageProvider(IHttpClient httpClient, IProviderManager providerManager, ILibraryManager libraryManager,
#if __JELLYFIN__
            ILoggerFactory logManager
#else
            ILogManager logManager
#endif
            , IJsonSerializer jsonSerializer, IFileSystem fileSystem, IApplicationPaths appPaths)
        {
            _httpClient = httpClient;
            this.providerManager = providerManager;
            this.libraryManager = libraryManager;
            _logger = logManager.CreateLogger<JavImageProvider>();
            _appPaths = appPaths;
            _jsonSerializer = jsonSerializer;
        }

        public string Name => Plugin.NAME;

        public async Task<DynamicImageResponse> GetImage(BaseItem item, ImageType type, CancellationToken cancellationToken)
        {
            _logger?.Info($"{nameof(GetImage)} type:{type} name:{item.Name}");

            var local = item.ImageInfos?.FirstOrDefault(o => o.Type == type && o.IsLocalFile);

            DynamicImageResponse GetResult()
            {
                var img = new DynamicImageResponse();
                if (local == null)
                    return img;

                img.Path = local.Path;
                img.Protocol = MediaProtocol.File;
                img.SetFormatFromMimeType(local.Path);
                img.HasImage = true;
                _logger?.Info($"{nameof(GetImage)} found.");
                return img;
            }

            JavVideoIndex index = null;
            if ((index = item.GetJavVideoIndex(_jsonSerializer)) == null)
            {
                _logger?.Info($"{nameof(GetImage)} name:{item.Name} JavVideoIndex not found.");
                return GetResult();
            }

            var metadata = Plugin.Instance.db.FindMetadata(index.Provider, index.Url);
            if (metadata == null || local?.DateModified.ToLocalTime() >= metadata.modified)
                return GetResult();

            var m = metadata?.data;

            if (string.IsNullOrWhiteSpace(m.Cover) && m.Samples?.Any() == true)
                m.Cover = m.Samples.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(m.Cover))
            {
                _logger?.Info($"{nameof(GetImage)} name:{item.Name} Cover not found.");
                return GetResult();
            }

            try
            {
                var resp = await ImageProxyService.GetImageResponse(m.Cover, type, cancellationToken);
                if (resp?.ContentLength > 0)
                {
#if __JELLYFIN__
                    await providerManager.SaveImage(item, resp.Content, resp.ContentType, type, 0, cancellationToken);
#else
                    await providerManager.SaveImage(item, libraryManager.GetLibraryOptions(item), resp.Content, resp.ContentType.ToArray(), type, 0, cancellationToken);
#endif

                    _logger.Info($"saved image: {type}");
                    local = item.ImageInfos?.FirstOrDefault(o => o.Type == type && o.IsLocalFile);
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn($"Save image error: {type} {m.Cover} {ex.Message}");
            }

            //转换本地文件
            return GetResult();
        }

        public
#if __JELLYFIN__
            System.Collections.Generic.IEnumerable<ImageType>
#else
            ImageType[]
#endif
            GetSupportedImages(BaseItem item)
              => new[] { ImageType.Primary, ImageType.Backdrop };

        public bool Supports(BaseItem item) => item is Movie;
    }
}