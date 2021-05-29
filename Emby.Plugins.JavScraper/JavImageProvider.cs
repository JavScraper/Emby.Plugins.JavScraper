using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Emby.Plugins.JavScraper.Scrapers;
using Emby.Plugins.JavScraper.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

#if __JELLYFIN__
using Microsoft.Extensions.Logging;
#else
using MediaBrowser.Model.Logging;
#endif

using MediaBrowser.Model.Serialization;

namespace Emby.Plugins.JavScraper
{
    public class JavImageProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly IProviderManager providerManager;
        private readonly ILibraryManager libraryManager;
        private readonly IServerApplicationHost serverApplicationHost;
        private readonly ImageProxyService imageProxyService;
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IApplicationPaths _appPaths;

        public int Order => 3;

        public JavImageProvider(IProviderManager providerManager, ILibraryManager libraryManager,
            IServerApplicationHost serverApplicationHost,
#if __JELLYFIN__
            ILoggerFactory logManager,
#else
            ILogManager logManager,
            ImageProxyService imageProxyService,
#endif
            IJsonSerializer jsonSerializer, IApplicationPaths appPaths)
        {
            this.providerManager = providerManager;
            this.libraryManager = libraryManager;
            this.serverApplicationHost = serverApplicationHost;
#if __JELLYFIN__
            imageProxyService = Plugin.Instance.ImageProxyService;
#else
            this.imageProxyService = imageProxyService;
#endif
            _logger = logManager.CreateLogger<JavImageProvider>();
            _appPaths = appPaths;
            _jsonSerializer = jsonSerializer;
        }

        public string Name => Plugin.NAME;

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            //  /emby/Plugins/JavScraper/Image?url=&type=xx
            var type = ImageType.Backdrop;
            if (url.IndexOf("Plugins/JavScraper/Image", StringComparison.OrdinalIgnoreCase) >= 0) //本地的链接
            {
                var uri = new Uri(url);
                var q = HttpUtility.ParseQueryString(uri.Query);
                var url2 = q["url"];
                if (url2.IsWebUrl())
                {
                    url = url2;
                    var tt = q.Get("type");
                    if (!string.IsNullOrWhiteSpace(tt) && Enum.TryParse<ImageType>(tt.Trim(), out var t2))
                        type = t2;
                }
            }
            _logger?.Info($"{nameof(GetImageResponse)} {url}");
            return imageProxyService.GetImageResponse(url, type, cancellationToken);
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item,
#if !__JELLYFIN__
            LibraryOptions libraryOptions,
#endif
            CancellationToken cancellationToken)
        {
            // ImageUrl = $"/emby/Plugins/JavScraper/Image?url={HttpUtility.UrlEncode(m.Cover)}",

            var list = new List<RemoteImageInfo>();

            JavVideoIndex index = null;
            if ((index = item.GetJavVideoIndex(_jsonSerializer)) == null)
            {
                _logger?.Info($"{nameof(GetImages)} name:{item.Name} JavVideoIndex not found.");
                return list;
            }

            var metadata = Plugin.Instance.db.FindMetadata(index.Provider, index.Url);
            if (metadata == null)
                return list;

            var m = metadata?.data;

            if (string.IsNullOrWhiteSpace(m.Cover) && m.Samples?.Any() == true)
                m.Cover = m.Samples.FirstOrDefault();

            var api_url = await serverApplicationHost.GetLocalApiUrl(default(CancellationToken));
            RemoteImageInfo Add(string url, ImageType type)
            {
                //http://127.0.0.1:{serverApplicationHost.HttpPort}
                var img = new RemoteImageInfo()
                {
                    Type = type,
                    ProviderName = Name,
                    Url = $"{api_url}/emby/Plugins/JavScraper/Image?url={HttpUtility.UrlEncode(m.Cover)}&type={type}"
                };
                list.Add(img);
                return img;
            }

            if (m.Cover.IsWebUrl())
            {
                Add(m.Cover, ImageType.Primary);
                Add(m.Cover, ImageType.Backdrop);
            }

            if (m.Samples?.Any() == true)
            {
                foreach (var url in m.Samples.Where(o => o.IsWebUrl()))
                    Add(url, ImageType.Screenshot);
            }

            return list;
        }

        public System.Collections.Generic.IEnumerable<ImageType> GetSupportedImages(BaseItem item)
               => new[] { ImageType.Primary, ImageType.Backdrop, ImageType.Screenshot };

        public bool Supports(BaseItem item) => item is Movie;
    }
}