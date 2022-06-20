using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Extensions;
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
    public sealed class JavImageProvider : IRemoteImageProvider, IHasOrder
    {
        private readonly IProviderManager _providerManager;
        private readonly ILibraryManager _libraryManager;
        private readonly ImageProxyService _imageProxyService;
        private readonly ILogger _logger;
        private readonly IApplicationPaths _appPaths;
        private readonly GfriendsAvatarService _gfriendsAvatarService;
        private readonly ApplicationDatabase _applicationDatabase;

        public JavImageProvider(
            IProviderManager providerManager,
            ILibraryManager libraryManager,
            ILoggerFactory loggerFactory,
            IApplicationPaths appPaths,
            ImageProxyService imageProxyService,
            GfriendsAvatarService gfriendsAvatarService,
            ApplicationDatabase applicationDatabase)
        {
            _logger = loggerFactory.CreateLogger<JavImageProvider>();
            _providerManager = providerManager;
            _libraryManager = libraryManager;
            _imageProxyService = imageProxyService;
            _gfriendsAvatarService = gfriendsAvatarService;
            _applicationDatabase = applicationDatabase;
            _appPaths = appPaths;
        }

        public int Order => 3;

        public string Name => "JavScraper";

        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
            => _imageProxyService.GetImageResponse(url, ImageType.Backdrop, cancellationToken);

        public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();

            RemoteImageInfo Add(string url, ImageType type)
            {
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
                    return Task.FromResult(list.AsEnumerable());
                }

                var metadata = _applicationDatabase.FindMetadata(index.Provider, index.Url);
                if (metadata == null)
                {
                    _logger.LogInformation("{Method} name:{Name} JavVideo not found from database.", nameof(GetImages), item.Name);
                    return Task.FromResult(list.AsEnumerable());
                }

                var vedio = metadata.Data;

                if (string.IsNullOrWhiteSpace(vedio.Cover) && vedio.Samples.Any())
                {
                    vedio.Cover = vedio.Samples.FirstOrDefault(string.Empty);
                }

                if (vedio.Cover.IsWebUrl())
                {
                    Add(vedio.Cover, ImageType.Primary);
                    Add(vedio.Cover, ImageType.Backdrop);
                }

                if (vedio.Samples.Any())
                {
                    foreach (var url in vedio.Samples.Where(o => o.IsWebUrl()))
                    {
                        Add(url, ImageType.Thumb);
                    }
                }
            }
            else if (item is Person)
            {
                _logger.LogInformation("{Method} name:{Name}.", nameof(GetImages), item.Name);

                var person = item.GetJavPerson();
                if (person == null)
                {
                    return Task.FromResult(list.AsEnumerable());
                }

                if (person.Cover.IsWebUrl())
                {
                    Add(person.Cover, ImageType.Primary);
                    Add(person.Cover, ImageType.Backdrop);
                }

                if (person.Samples.Any())
                {
                    foreach (var url in person.Samples.Where(sample => sample.IsWebUrl()))
                    {
                        Add(url, ImageType.Thumb);
                    }
                }
            }

            return Task.FromResult(list.AsEnumerable());
        }

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
               => new[] { ImageType.Primary, ImageType.Backdrop, ImageType.Thumb };

        public bool Supports(BaseItem item) => item is Movie || item is Person;
    }
}
