using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JavScraper.Extensions;
using Jellyfin.Plugin.JavScraper.Http;
using Jellyfin.Plugin.JavScraper.Scrapers.Model;
using Jellyfin.Plugin.JavScraper.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Providers
{
    public sealed class JavPersonProvider : IRemoteMetadataProvider<Person, PersonLookupInfo>, IHasOrder
    {
        private const string Lang = "zh";
        private static readonly Regex _birthdayRegex = new(@"出生[^\d]*(?<y>[\d]+)年(?<m>[\d]+)月(?<d>[\d]+)日");
        private readonly Uri _baseUrl = new("https://xslist.org");
        private readonly ILogger _logger;
        private readonly TranslationService _translationService;
        private readonly ImageProxyService _imageProxyService;
        private readonly GfriendsAvatarService _gfriendsAvatarService;
        private readonly IProviderManager _providerManager;
        private readonly IApplicationPaths _appPaths;
        private readonly ICustomHttpClientFactory _clientFactory;

        public JavPersonProvider(
            ILoggerFactory loggerFactory,
            IProviderManager providerManager,
            IApplicationPaths appPaths,
            ImageProxyService imageProxyService,
            GfriendsAvatarService gfriendsAvatarService,
            TranslationService translationService,
            ICustomHttpClientFactory clientFactory)
        {
            _logger = loggerFactory.CreateLogger<JavPersonProvider>();
            _translationService = translationService;
            _imageProxyService = imageProxyService;
            _gfriendsAvatarService = gfriendsAvatarService;
            _providerManager = providerManager;
            _appPaths = appPaths;
            _clientFactory = clientFactory;
        }

        public int Order => 4;

        public string Name => "JavScraper.Xslist";

        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
            => _imageProxyService.GetImageResponse(url, ImageType.Primary, cancellationToken);

        public async Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo info, CancellationToken cancellationToken)
        {
            var metadataResult = new MetadataResult<Person>();
            JavPersonIndex? index = null;

            _logger.LogInformation("{Method} info:{Info}", nameof(GetMetadata), JsonSerializer.Serialize(info));

            if ((index = info.GetJavPersonIndex()) == null || !index.Url.Contains("xslist.org", StringComparison.OrdinalIgnoreCase) || !index.Url.IsWebUrl())
            {
                var searchResults = await GetSearchResults(info, cancellationToken).ConfigureAwait(false);
                if (!searchResults.Any() || (index = searchResults.FirstOrDefault()?.GetJavPersonIndex()) == null)
                {
                    _logger.LogInformation("{Method} name:{Name} not found 0.", nameof(GetMetadata), info.Name);
                    return metadataResult;
                }
            }

            if (index == null || !index.Url.IsWebUrl())
            {
                _logger.LogInformation("{Method} name:{Name} not found 1.", nameof(GetMetadata), info.Name);
                return metadataResult;
            }

            var doc = await _clientFactory.GetClient().GetHtmlDocumentAsync(new Uri(_baseUrl, index.Url)).ConfigureAwait(false);
            if (doc == null)
            {
                _logger.LogInformation("{Method} name:{Name} GetHtmlDocumentAsync failed.", nameof(GetMetadata), info.Name);
                return metadataResult;
            }

            var node = doc.DocumentNode.SelectSingleNode("//*[@itemprop='name']");

            if (node == null)
            {
                _logger.LogInformation("{Method} name:{Name} name node not found.", nameof(GetMetadata), info.Name);
                return metadataResult;
            }

            var name = node.InnerText;
            var additionalName = doc.DocumentNode.SelectSingleNode("//*[@itemprop='additionalName']/..");

            index.Samples = doc.DocumentNode.SelectNodes("//*[@id='gallery']/a")?
                .Select(o => o.GetAttributeValue("href", null))
                .Where(o => o.IsWebUrl()).ToList()
                ?? new List<string>(0);

            index.Cover = await _gfriendsAvatarService.FindAvatarAddressAsync(name, cancellationToken).ConfigureAwait(false) ?? string.Empty;
            node = doc.DocumentNode.SelectSingleNode("//*[@itemprop='nationality']");
            if (node != null)
            {
                node = node.ParentNode;
            }
            else
            {
                node = doc.DocumentNode.SelectSingleNode("//p[contains(text(),'出道日期:')]");
            }

            var cut_persion_image = Plugin.Instance?.Configuration?.EnableCutPersonImage ?? true;
            var person_image_type = cut_persion_image ? ImageType.Primary : ImageType.Backdrop;

            metadataResult.HasMetadata = true;
            metadataResult.QueriedById = true;

            var overview = node?.InnerText;
            // var additionalNames = names?.Any() == true ? $"别名：{string.Join(", ", names)}\n" : null;
            metadataResult.Item = new Person
            {
                OfficialRating = "XXX",
                Name = name,
                Overview = additionalName?.OuterHtml + node?.OuterHtml,
                OriginalTitle = name,
                SortName = name,
                ForcedSortName = name,
                ExternalId = name
            };

            metadataResult.Item.SetJavPersonIndex(index);

            if (!string.IsNullOrWhiteSpace(overview))
            {
                // PremiereDate = actress.Birthday,
                // ProductionYear = actress.Birthday?.Year,
                // ProductionLocations = locations.ToArray(),

                var match = _birthdayRegex.Match(overview);
                if (match.Success)
                {
                    var birthday = new DateTime(
                        int.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture),
                        int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture),
                        int.Parse(match.Groups["d"].Value, CultureInfo.InvariantCulture));
                    metadataResult.Item.PremiereDate = birthday;
                    metadataResult.Item.ProductionYear = birthday.Year;
                }
            }

            node = doc.DocumentNode.SelectSingleNode("//*[@itemprop='nationality']");
            if (node != null)
            {
                var n = node.InnerText;
                if (!string.IsNullOrWhiteSpace(n))
                {
                    metadataResult.Item.ProductionLocations = new[] { n };
                }
            }

            return metadataResult;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(PersonLookupInfo searchInfo, CancellationToken cancellationToken)
        {
            var list = new List<RemoteSearchResult>();
            if (string.IsNullOrWhiteSpace(searchInfo.Name))
            {
                return list;
            }

            // 多重名字的 晶エリー（新井エリー、大沢佑香）
            var names = searchInfo.Name.Split("（）()、".ToArray(), StringSplitOptions.RemoveEmptyEntries)
                  .Select(o => o.Trim())
                  .Where(o => !string.IsNullOrEmpty(o)).ToArray();

            foreach (var search_name in names)
            {
                _logger.LogInformation("{} name: {}", nameof(GetSearchResults), search_name);

                var url = $"/search?query={search_name}&lg={Lang}";

                var doc = await _clientFactory.GetClient().GetHtmlDocumentAsync(url).ConfigureAwait(false);
                if (doc == null)
                {
                    continue;
                }

                var aNodes = doc.DocumentNode.SelectNodes("//li/h3/a");
                if (aNodes == null || !aNodes.Any())
                {
                    continue;
                }

                foreach (var aNode in aNodes)
                {
                    var name = aNode.InnerText?.Trim();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var p = aNode.ParentNode.ParentNode;
                    var imgUrl = p.SelectSingleNode(".//img")?.GetAttributeValue("href", null) ?? string.Empty;
                    var result = new RemoteSearchResult
                    {
                        Name = name,
                        ImageUrl = imgUrl.IsWebUrl() ? _imageProxyService.GetLocalUrl(imgUrl, withApiUrl: false) : null,
                        SearchProviderName = Name,
                        Overview = p.SelectSingleNode("./p").InnerText,
                    };
                    var m = new JavPersonIndex()
                    {
                        Provider = Name,
                        Name = name,
                        ImageType = ImageType.Backdrop,
                        Url = aNode.GetAttributeValue("href", string.Empty),
                        Cover = await _gfriendsAvatarService.FindAvatarAddressAsync(name, cancellationToken).ConfigureAwait(false) ?? string.Empty
                    };
                    result.SetJavPersonIndex(m);
                    list.Add(result);
                }

                if (searchInfo.IsAutomated && list.Any())
                {
                    return list;
                }
            }

            return list;
        }
    }
}
