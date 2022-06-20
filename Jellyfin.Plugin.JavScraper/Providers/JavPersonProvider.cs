using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JavScraper.Extensions;
using Jellyfin.Plugin.JavScraper.Http;
using Jellyfin.Plugin.JavScraper.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Providers
{
    public sealed class JavPersonProvider : IRemoteMetadataProvider<Person, PersonLookupInfo>, IHasOrder
    {
        private readonly ILogger _logger;
        private readonly ImageProxyService _imageProxyService;
        private readonly PersonSearchService _personSearchService;
        private readonly IHttpClientManager _clientFactory;

        public JavPersonProvider(
            ILogger<JavPersonProvider> logger,
            ImageProxyService imageProxyService,
            PersonSearchService personSearchService,
            IHttpClientManager clientFactory)
        {
            _logger = logger;
            _imageProxyService = imageProxyService;
            _personSearchService = personSearchService;
            _clientFactory = clientFactory;
        }

        public int Order => 4;

        public string Name => "JavScraper.Xslist";

        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
            => _imageProxyService.GetImageResponse(url, ImageType.Primary, cancellationToken);

        public async Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo info, CancellationToken cancellationToken)
        {
            _logger.LogInformation("call {Method}, {Args}", nameof(GetSearchResults), $"{nameof(info)}={info.ToJson()}");

            var metadataResult = new MetadataResult<Person>();
            var index = info.GetJavPersonIndex();

            if (index == null || !index.Url.Contains("xslist.org", StringComparison.OrdinalIgnoreCase) || !index.Url.IsWebUrl())
            {
                _logger.LogInformation("Could not find index from info={Info}, try to search!", info.ToJson());
                var searchResults = await _personSearchService.SearchPersonByName(info.Name).ConfigureAwait(false);
                index = searchResults.FirstOrDefault();
            }

            if (index == null || !index.Url.IsWebUrl())
            {
                _logger.LogInformation("Could not find index for info={Info}, exit!", info.ToJson());
                return metadataResult;
            }

            var person = await _personSearchService.GetDetail(index).ConfigureAwait(false);

            if (person == null)
            {
                _logger.LogInformation("Could not find person for index={Index}, exit!", index);
                return metadataResult;
            }

            metadataResult.Item = new Person
            {
                Name = person.Name,
                HomePageUrl = person.Url,
                Overview = person.Overview,
                OriginalTitle = person.Name,
                SortName = person.Name,
                ForcedSortName = person.Name,
                ExternalId = person.Name,
                ProductionLocations = string.IsNullOrWhiteSpace(person.Nationality) ? null : new string[] { person.Nationality },
                PremiereDate = person.Birthday,
                ProductionYear = person.Birthday?.Year
            };

            metadataResult.Item.SetJavPersonIndex(index);
            metadataResult.HasMetadata = true;
            metadataResult.QueriedById = true;

            return metadataResult;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(PersonLookupInfo searchInfo, CancellationToken cancellationToken)
        {
            _logger.LogInformation("call {Method}, {Args}", nameof(GetSearchResults), $"{nameof(searchInfo)}={searchInfo.ToJson()}");
            if (string.IsNullOrWhiteSpace(searchInfo.Name))
            {
                return Array.Empty<RemoteSearchResult>();
            }

            var indexList = await _personSearchService.SearchPersonByName(searchInfo.Name).ConfigureAwait(false);
            return indexList.Select(index =>
            {
                var result = new RemoteSearchResult
                {
                    Name = index.Name,
                    ImageUrl = index.Avatar.IsWebUrl() ? _imageProxyService.GetLocalUrl(index.Avatar, withApiUrl: false) : null,
                    SearchProviderName = Name,
                    Overview = index.Overview,
                };
                result.SetJavPersonIndex(index);
                return result;
            });
        }
    }
}
