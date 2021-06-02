using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugins.JavScraper.Scrapers;
using Emby.Plugins.JavScraper.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
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
        private readonly ImageProxyService imageProxyService;
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IApplicationPaths _appPaths;
        public Gfriends Gfriends { get; }

        public int Order => 3;

        public JavImageProvider(IProviderManager providerManager, ILibraryManager libraryManager,
#if __JELLYFIN__
            ILoggerFactory logManager,
#else
            ILogManager logManager,
            ImageProxyService imageProxyService,
            Gfriends gfriends,
#endif
            IJsonSerializer jsonSerializer, IApplicationPaths appPaths)
        {
            this.providerManager = providerManager;
            this.libraryManager = libraryManager;
#if __JELLYFIN__
            imageProxyService = Plugin.Instance.ImageProxyService;
            Gfriends = new Gfriends(logManager, _jsonSerializer);
#else
            this.imageProxyService = imageProxyService;
            Gfriends = gfriends;
#endif
            _logger = logManager.CreateLogger<JavImageProvider>();
            _appPaths = appPaths;
            _jsonSerializer = jsonSerializer;
        }

        public string Name => Plugin.NAME;

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
            => imageProxyService.GetImageResponse(url, ImageType.Backdrop, cancellationToken);

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item,
#if !__JELLYFIN__
            LibraryOptions libraryOptions,
#endif
            CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();

            async Task<RemoteImageInfo> Add(string url, ImageType type)
            {
                //http://127.0.0.1:{serverApplicationHost.HttpPort}
                var img = new RemoteImageInfo()
                {
                    Type = type,
                    ProviderName = Name,
                    Url = await imageProxyService.GetLocalUrl(url, type)
                };
                list.Add(img);
                return img;
            }

            if (item is Movie)
            {
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

                if (m.Cover.IsWebUrl())
                {
                    await Add(m.Cover, ImageType.Primary);
                    await Add(m.Cover, ImageType.Backdrop);
                }

                if (m.Samples?.Any() == true)
                {
                    foreach (var url in m.Samples.Where(o => o.IsWebUrl()))
                        await Add(url, ImageType.Thumb);
                }
            }
            else if (item is Person)
            {
                _logger?.Info($"{nameof(GetImages)} name:{item.Name}.");

                JavPersonIndex index = null;
                if ((index = item.GetJavPersonIndex(_jsonSerializer)) == null)
                {
                    var cover = await Gfriends.FindAsync(item.Name, cancellationToken);
                    _logger?.Info($"{nameof(GetImages)} name:{item.Name} Gfriends: {cover}.");

                    if (cover.IsWebUrl() != true)
                        return list;

                    index = new JavPersonIndex() { Cover = cover };
                }

                if (!index.Cover.IsWebUrl())
                {
                    index.Cover = await Gfriends.FindAsync(item.Name, cancellationToken);
                    if (string.IsNullOrWhiteSpace(index.Cover))
                        return list;
                }

                if (index.Cover.IsWebUrl())
                {
                    await Add(index.Cover, ImageType.Primary);
                    await Add(index.Cover, ImageType.Backdrop);
                }

                if (index.Samples?.Any() == true)
                {
                    foreach (var url in index.Samples.Where(o => o.IsWebUrl()))
                        await Add(url, ImageType.Thumb);
                }
            }

            return list;
        }

        public System.Collections.Generic.IEnumerable<ImageType> GetSupportedImages(BaseItem item)
               => new[] { ImageType.Primary, ImageType.Backdrop, ImageType.Thumb };

        public bool Supports(BaseItem item) => item is Movie || item is Person;
    }
}