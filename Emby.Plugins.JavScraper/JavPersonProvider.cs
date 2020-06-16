using Emby.Plugins.JavScraper.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugins.JavScraper.Scrapers;

namespace Emby.Plugins.JavScraper
{
    public class JavPersonProvider : IRemoteMetadataProvider<Person, PersonLookupInfo>, IHasOrder
    {
        private readonly ILogger _logger;
        private readonly IProviderManager providerManager;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IApplicationPaths _appPaths;

        public int Order => 4;

        public string Name => Plugin.NAME + "-Actress";

        public ImageProxyService ImageProxyService { get; }

        public Gfriends Gfriends { get; }

        public JavPersonProvider(
#if __JELLYFIN__
            ILoggerFactory logManager
#else
            ILogManager logManager
#endif
            , IProviderManager providerManager, IJsonSerializer jsonSerializer, IFileSystem fileSystem, IApplicationPaths appPaths)
        {
            _logger = logManager.CreateLogger<JavPersonProvider>();
            this.providerManager = providerManager;
            _jsonSerializer = jsonSerializer;
            _appPaths = appPaths;
            ImageProxyService = new ImageProxyService(jsonSerializer, logManager.CreateLogger<ImageProxyService>(), fileSystem, appPaths);
            Gfriends = new Gfriends(logManager.CreateLogger<Gfriends>(), _jsonSerializer);
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(PersonLookupInfo info, CancellationToken cancelationToken)
        {
            var list = new List<RemoteSearchResult>();
            if (string.IsNullOrWhiteSpace(info.Name))
                return list;

            var url = await Gfriends.FindAsync(info.Name, cancelationToken);

            if (string.IsNullOrWhiteSpace(url))
                return list;

            var result = new RemoteSearchResult
            {
                Name = info.Name,
                ImageUrl = $"/emby/Plugins/JavScraper/Image?url={url}",
                SearchProviderName = Name,
            };
            result.ProviderIds[Name] = url;
            list.Add(result);

            return list;
        }

        public async Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo info, CancellationToken cancellationToken)
        {
            var metadataResult = new MetadataResult<Person>();

            var url = await Gfriends.FindAsync(info.Name, cancellationToken);

            if (url == null)
            {
                _logger?.Info($"{nameof(GetMetadata)} name:{info.Name} not found.");
                return metadataResult;
            }

            metadataResult.HasMetadata = true;

            metadataResult.Item = new Person()
            {
                ProviderIds = new Dictionary<string, string> { { Name, url } },
                Overview = "\u200B"
            };

            return metadataResult;
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancelToken)
        {
            //  /emby/Plugins/JavScraper/Image?url=
            if (url.IndexOf("Plugins/JavScraper/Image", StringComparison.OrdinalIgnoreCase) >= 0) //本地的链接
            {
                var start = url.IndexOf('=');
                url = url.Substring(start + 1);
                if (url.Contains("://") == false)
                    url = WebUtility.UrlDecode(url);
            }
            _logger?.Info($"{nameof(GetImageResponse)} {url}");
            return ImageProxyService.GetImageResponse(url, ImageType.Backdrop, cancelToken);
        }
    }
}