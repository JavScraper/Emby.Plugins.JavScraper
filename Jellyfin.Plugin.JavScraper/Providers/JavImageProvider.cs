using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Extensions;
using Jellyfin.Plugin.JavScraper.Scrapers.Model;
using Jellyfin.Plugin.JavScraper.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Providers
{
    public sealed class JavImageProvider : IRemoteImageProvider, IHasOrder, IDisposable
    {
        private readonly IProviderManager _providerManager;
        private readonly ILibraryManager _libraryManager;
        private readonly ImageProxyService _imageProxyService;
        private readonly ILogger _logger;
        private readonly IApplicationPaths _appPaths;
        private readonly GfriendsAvatarService _gfriendsAvatarService;
        private readonly ApplicationDbContext _applicationDbContext;

        public JavImageProvider(
            IProviderManager providerManager,
            ILibraryManager libraryManager,
            ILoggerFactory loggerFactory,
            IApplicationPaths appPaths,
            ImageProxyService imageProxyService,
            GfriendsAvatarService gfriendsAvatarService,
            ApplicationDbContext applicationDbContext)
        {
            _logger = loggerFactory.CreateLogger<JavImageProvider>();
            _providerManager = providerManager;
            _libraryManager = libraryManager;
            _imageProxyService = imageProxyService;
            _gfriendsAvatarService = gfriendsAvatarService;
            _applicationDbContext = applicationDbContext;
            _appPaths = appPaths;
        }

        public int Order => 3;

        public string Name => "JavScraper";

        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
            => _imageProxyService.GetImageResponse(url, ImageType.Backdrop, cancellationToken);

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();

            RemoteImageInfo Add(string url, ImageType type)
            {
                // http://127.0.0.1:{serverApplicationHost.HttpPort}
                var img = new RemoteImageInfo()
                {
                    Type = type,
                    ProviderName = Name,
                    Url = _imageProxyService.GetLocalUrl(url, type)
                };
                list.Add(img);
                return img;
            }

            if (item is Movie)
            {
                var index = item.GetJavVideoIndex();
                if (index == null)
                {
                    _logger.LogInformation("{Method} name:{Name} JavVideoIndex not found.", nameof(GetImages), item.Name);
                    return list;
                }

                var metadata = _applicationDbContext.FindMetadata(index.Provider, index.Url);
                if (metadata == null)
                {
                    return list;
                }

                var m = metadata.Data;

                if (string.IsNullOrWhiteSpace(m.Cover) && m.Samples.Any())
                {
                    m.Cover = m.Samples.FirstOrDefault(string.Empty);
                }

                if (m.Cover.IsWebUrl())
                {
                    Add(m.Cover, ImageType.Primary);
                    Add(m.Cover, ImageType.Backdrop);
                }

                if (m.Samples?.Any() == true)
                {
                    foreach (var url in m.Samples.Where(o => o.IsWebUrl()))
                    {
                        Add(url, ImageType.Thumb);
                    }
                }
            }
            else if (item is Person)
            {
                _logger.LogInformation("{Method} name:{Name}.", nameof(GetImages), item.Name);

                var index = item.GetJavPersonIndex();
                if (index == null)
                {
                    var cover = await _gfriendsAvatarService.FindAvatarAddressAsync(item.Name, cancellationToken).ConfigureAwait(false) ?? string.Empty;
                    _logger.LogInformation("{Method} name={Name} cover={Cover}.", nameof(GetImages), item.Name, cover);

                    if (!cover.IsWebUrl())
                    {
                        return list;
                    }

                    index = new JavPersonIndex() { Cover = cover };
                }

                if (!index.Cover.IsWebUrl())
                {
                    index.Cover = await _gfriendsAvatarService.FindAvatarAddressAsync(item.Name, cancellationToken).ConfigureAwait(false) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(index.Cover))
                    {
                        return list;
                    }
                }

                if (index.Cover.IsWebUrl())
                {
                    Add(index.Cover, ImageType.Primary);
                    Add(index.Cover, ImageType.Backdrop);
                }

                if (index.Samples.Any())
                {
                    foreach (var url in index.Samples.Where(o => o.IsWebUrl()))
                    {
                        Add(url, ImageType.Thumb);
                    }
                }
            }

            return list;
        }

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
               => new[] { ImageType.Primary, ImageType.Backdrop, ImageType.Thumb };

        public bool Supports(BaseItem item) => item is Movie || item is Person;

        public void Dispose()
        {
            _gfriendsAvatarService.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
