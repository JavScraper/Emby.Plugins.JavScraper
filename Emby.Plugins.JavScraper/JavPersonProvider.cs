using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugins.JavScraper.Http;
using Emby.Plugins.JavScraper.Scrapers;
using Emby.Plugins.JavScraper.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

#if __JELLYFIN__
using Microsoft.Extensions.Logging;
#else
using MediaBrowser.Model.Logging;
#endif

using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace Emby.Plugins.JavScraper
{
    public class JavPersonProvider : IRemoteMetadataProvider<Person, PersonLookupInfo>, IHasOrder
    {
        private readonly ILogger _logger;
        private readonly TranslationService translationService;
        private readonly ImageProxyService imageProxyService;
        private readonly IProviderManager providerManager;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IApplicationPaths _appPaths;
        protected HttpClientEx client;

        private const string base_url = "https://xslist.org/";
        private const string lg = "zh";

        public Gfriends Gfriends { get; }

        public JavPersonProvider(
#if __JELLYFIN__
            ILoggerFactory logManager,
#else
            ILogManager logManager,
            TranslationService translationService,
            ImageProxyService imageProxyService,
            Gfriends gfriends,
#endif
            IProviderManager providerManager, IJsonSerializer jsonSerializer, IApplicationPaths appPaths)
        {
            _logger = logManager.CreateLogger<JavPersonProvider>();
#if __JELLYFIN__
            translationService = Plugin.Instance.TranslationService;
            imageProxyService = Plugin.Instance.ImageProxyService;
            Gfriends = new Gfriends(logManager, _jsonSerializer);
#else
            this.translationService = translationService;
            this.imageProxyService = imageProxyService;
            Gfriends = gfriends;
#endif
            this.providerManager = providerManager;
            _jsonSerializer = jsonSerializer;
            _appPaths = appPaths;
            client = new HttpClientEx(client => client.BaseAddress = new Uri(base_url));
        }

        public int Order => 4;

        public string Name => Plugin.NAME + ".Xslist";

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
            => imageProxyService.GetImageResponse(url, ImageType.Primary, cancellationToken);

        private static Regex regexBirthday = new Regex(@"出生[^\d]*(?<y>[\d]+)年(?<m>[\d]+)月(?<d>[\d]+)日");

        public async Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo info, CancellationToken cancellationToken)
        {
            var metadataResult = new MetadataResult<Person>();
            JavPersonIndex index = null;

            _logger?.Info($"{nameof(GetMetadata)} info:{_jsonSerializer.SerializeToString(info)}");

            if ((index = info.GetJavPersonIndex(_jsonSerializer)) == null || index.Url?.Contains("xslist.org", StringComparison.OrdinalIgnoreCase) != true || !index.Url.IsWebUrl())
            {
                var res = await GetSearchResults(info, cancellationToken).ConfigureAwait(false);
                if (res.Count() == 0 || (index = res.FirstOrDefault().GetJavPersonIndex(_jsonSerializer)) == null)
                {
                    _logger?.Info($"{nameof(GetMetadata)} name:{info.Name} not found 0.");
                    return metadataResult;
                }
            }

            if (index?.Url.IsWebUrl() != true)
            {
                _logger?.Info($"{nameof(GetMetadata)} name:{info.Name} not found 1.");
                return metadataResult;
            }

            var doc = await AbstractScraper.GetHtmlDocumentAsync(client, index.Url, _logger);
            if (doc == null)
            {
                _logger?.Info($"{nameof(GetMetadata)} name:{info.Name} GetHtmlDocumentAsync failed.");
                return metadataResult;
            }

            var node = doc.DocumentNode.SelectSingleNode("//*[@itemprop='name']");

            if (node == null)
            {
                _logger?.Info($"{nameof(GetMetadata)} name:{info.Name} name node not found.");
                return metadataResult;
            }

            var name = node.InnerText;
            var additionalName = doc.DocumentNode.SelectSingleNode("//*[@itemprop='additionalName']/..");

            index.Samples = doc.DocumentNode.SelectNodes("//*[@id='gallery']/a")?
                 .Select(o => o.GetAttributeValue("href", null))
                 .Where(o => o.IsWebUrl()).ToList();

            index.Cover = await Gfriends.FindAsync(name, cancellationToken);
            node = doc.DocumentNode.SelectSingleNode("//*[@itemprop='nationality']");
            if (node != null)
                node = node.ParentNode;
            else
                node = doc.DocumentNode.SelectSingleNode("//p[contains(text(),'出道日期:')]");

            var cut_persion_image = Plugin.Instance?.Configuration?.EnableCutPersonImage ?? true;
            var person_image_type = cut_persion_image ? ImageType.Primary : ImageType.Backdrop;

            metadataResult.HasMetadata = true;
            metadataResult.QueriedById = true;

            var overview = node?.InnerText;
            //var additionalNames = names?.Any() == true ? $"别名：{string.Join(", ", names)}\n" : null;
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

            metadataResult.Item.SetJavPersonIndex(_jsonSerializer, index);

            if (!string.IsNullOrWhiteSpace(overview))
            {
                //PremiereDate = actress.Birthday,
                //    ProductionYear = actress.Birthday?.Year,
                //    ProductionLocations = locations.ToArray(),

                var m = regexBirthday.Match(overview);
                if (m.Success)
                {
                    var birthday = new DateTime(int.Parse(m.Groups["y"].Value), int.Parse(m.Groups["m"].Value), int.Parse(m.Groups["d"].Value));
                    metadataResult.Item.PremiereDate = birthday;
                    metadataResult.Item.ProductionYear = birthday.Year;
                }
            }
            node = doc.DocumentNode.SelectSingleNode("//*[@itemprop='nationality']");
            if (node != null)
            {
                var n = node.InnerText;
                if (!string.IsNullOrWhiteSpace(n))
                    metadataResult.Item.ProductionLocations = new[] { n };
            }

            return metadataResult;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(PersonLookupInfo searchInfo, CancellationToken cancellationToken)
        {
            var list = new List<RemoteSearchResult>();
            if (string.IsNullOrWhiteSpace(searchInfo.Name))
                return list;
            //多重名字的 晶エリー（新井エリー、大沢佑香）
            var names = searchInfo.Name.Split("（）()、".ToArray(), StringSplitOptions.RemoveEmptyEntries)
                  .Select(o => o.Trim())
                  .Where(o => !string.IsNullOrEmpty(o)).ToArray();

            foreach (var search_name in names)
            {
                _logger?.Info($"{nameof(GetSearchResults)} name: {search_name}");

                var url = $"/search?query={search_name}&lg={lg}";

                var doc = await AbstractScraper.GetHtmlDocumentAsync(client, url, _logger);
                if (doc == null)
                    continue;

                var nodes = doc.DocumentNode.SelectNodes("//li/h3/a");
                if (nodes?.Any() != true)
                    continue;

                foreach (var node in nodes)
                {
                    var name = node.InnerText?.Trim();
                    var p = node.ParentNode.ParentNode;
                    var img = p.SelectSingleNode(".//img")?.GetAttributeValue("href", string.Empty);
                    var result = new RemoteSearchResult
                    {
                        Name = name,
                        ImageUrl = img.IsWebUrl() ? await imageProxyService.GetLocalUrl(img, with_api_url: false) : null,
                        SearchProviderName = Name,
                        Overview = p.SelectSingleNode("./p").InnerText,
                    };
                    var m = new JavPersonIndex()
                    {
                        Provider = Name,
                        Name = name,
                        ImageType = ImageType.Backdrop,
                        Url = node.GetAttributeValue("href", string.Empty),
                        Cover = await Gfriends.FindAsync(name, cancellationToken)
                    };
                    result.SetJavPersonIndex(_jsonSerializer, m);
                    list.Add(result);
                }
                if (searchInfo.IsAutomated && list.Any())
                    return list;
            }

            return list;
        }
    }
}