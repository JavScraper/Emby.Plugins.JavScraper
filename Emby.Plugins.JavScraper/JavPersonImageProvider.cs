using Emby.Plugins.JavScraper.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.MediaInfo;

namespace Emby.Plugins.JavScraper
{
    public class JavPersonImageProvider : IDynamicImageProvider
    {
        private readonly IHttpClient _httpClient;
        private readonly IProviderManager providerManager;
        private readonly ILibraryManager libraryManager;
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IApplicationPaths _appPaths;
        public ImageProxyService ImageProxyService { get; }

        public JavPersonImageProvider(IHttpClient httpClient, IProviderManager providerManager, ILibraryManager libraryManager,
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
            _logger = logManager.CreateLogger<JavPersonImageProvider>();
            _appPaths = appPaths;
            _jsonSerializer = jsonSerializer;
            ImageProxyService = new ImageProxyService(jsonSerializer, logManager.CreateLogger<ImageProxyService>(), fileSystem, appPaths);
        }

        public string Name => JavPersonProvider.NAME;

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

            if (local != null)
                return GetResult();

            var index = item.GetJavPersonIndex(_jsonSerializer);
            var url = index?.Url;
            if (string.IsNullOrWhiteSpace(url))
            {
                _logger?.Info($"{nameof(GetImage)} name:{item.Name} Url not found.");
                return GetResult();
            }

            try
            {
                var enable = Plugin.Instance?.Configuration?.EnableCutPersonImage ?? true;
                var image_type = enable ? type : ImageType.Backdrop;
                var resp = await ImageProxyService.GetImageResponse(url, image_type, cancellationToken);
                if (resp?.ContentLength > 0)
                {
#if __JELLYFIN__
                    await providerManager.SaveImage(item, resp.Content, resp.ContentType, type, 0, cancellationToken);
#else
                    await providerManager.SaveImage(item, libraryManager.GetLibraryOptions(item), resp.Content, resp.ContentType.ToArray(), type, 0, cancellationToken);
#endif

                    _logger.Info($"saved image: {type}");
                    local = item.ImageInfos?.FirstOrDefault(o => o.Type == type && o.IsLocalFile);
                    if (index.ImageType != image_type)
                    {
                        index.ImageType = image_type;
                        item.SetJavPersonIndex(_jsonSerializer, index);
                        item.UpdateToRepository(ItemUpdateType.MetadataEdit);
                    }


                }
            }
            catch (Exception ex)
            {
                _logger?.Warn($"Save image error: {type} {url} {ex.Message}");
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
              => new[] { ImageType.Primary };

        public bool Supports(BaseItem item) => item is Person;
    }
}