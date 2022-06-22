using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JavScraper.Extensions;
using Jellyfin.Plugin.JavScraper.Http;
using Jellyfin.Plugin.JavScraper.Scrapers.Model;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Services
{
    public class PersonSearchService
    {
        private const string Lang = "zh";
        private static readonly Regex _birthdayRegex = new(@"出生[^\d]*(?<date>\d+年\d+月\d+日)");
        private readonly Uri _baseUrl = new("https://xslist.org");
        private readonly ILogger _logger;
        private readonly GfriendsAvatarService _gfriendsAvatarService;
        private readonly IHttpClientManager _clientManager;

        public PersonSearchService(
            ILogger<PersonSearchService> logger,
            GfriendsAvatarService gfriendsAvatarService,
            IHttpClientManager httpClientManager)
        {
            _logger = logger;
            _gfriendsAvatarService = gfriendsAvatarService;
            _clientManager = httpClientManager;
        }

        public async Task<IEnumerable<JavPersonIndex>> SearchPersonByName(string searchName)
        {
            // 多重名字的 晶エリー（新井エリー、大沢佑香）
            _logger.LogInformation("call {Method}, {Args}", nameof(SearchPersonByName), $"{nameof(searchName)}={searchName}");
            var searchTasks = searchName.Split("（）()、".ToArray(), StringSplitOptions.TrimEntries)
                .Select(name =>
                {
                    var url = new Uri(_baseUrl, $"/search?query={name}&lg={Lang}");
                    return _clientManager.GetClient().GetHtmlDocumentAsync(url);
                })
                .ToArray();

            var documents = await Task.WhenAll(searchTasks).ConfigureAwait(false);

            return documents
                .SelectMany(document =>
                {
                    if (document == null)
                    {
                        return Enumerable.Empty<JavPersonIndex>();
                    }

                    var nodes = document.DocumentNode.SelectNodes("//li");
                    if (nodes == null)
                    {
                        return Enumerable.Empty<JavPersonIndex>();
                    }

                    return nodes
                        .Select(node => new JavPersonIndex()
                        {
                            Name = node.SelectSingleNode(".//a")?.InnerText.Trim() ?? string.Empty, Url = node.SelectSingleNode(".//a")?.GetAttributeValue("href", null) ?? string.Empty, Avatar = node.SelectSingleNode(".//img")?.GetAttributeValue("src", null) ?? string.Empty, Overview = node.SelectSingleNode(".//p")?.InnerText.Trim() ?? string.Empty
                        })
                        .Where(person => !string.IsNullOrWhiteSpace(person.Name));
                });
        }

        public async Task<JavPerson?> GetDetail(JavPersonIndex index)
        {
            var doc = await _clientManager.GetClient().GetHtmlDocumentAsync(new Uri(_baseUrl, index.Url)).ConfigureAwait(false);
            if (doc == null)
            {
                return null;
            }

            var name = doc.DocumentNode.SelectSingleNode("//*[@itemprop='name']")?.InnerText.Trim();

            if (name == null)
            {
                return null;
            }

            var additionalName = doc.DocumentNode.SelectSingleNode("//*[@itemprop='additionalName']/..");
            var node = doc.DocumentNode.SelectSingleNode("//*[@itemprop='nationality']/..") ?? doc.DocumentNode.SelectSingleNode("//p[contains(text(),'出道日期:')]");

            return new JavPerson
            {
                Url = index.Url,
                Avatar = index.Avatar,
                Name = name,
                Birthday = node?.InnerText.TryMatch(_birthdayRegex, out var match) == true ? DateTime.ParseExact(match.Groups["date"].Value, "yyyy年M月d日", CultureInfo.CurrentCulture, DateTimeStyles.AllowInnerWhite) : null,
                Overview = additionalName?.OuterHtml + node?.OuterHtml,
                Cover = await _gfriendsAvatarService.FindAvatarAddressAsync(name, CancellationToken.None).ConfigureAwait(false) ?? string.Empty,
                Samples = doc.DocumentNode.SelectNodes("//*[@id='gallery']/a")?.Select(o => o.GetAttributeValue("href", string.Empty)).Where(href => href.IsWebUrl()).ToArray() ?? Array.Empty<string>(),
                Nationality = doc.DocumentNode.SelectSingleNode("//*[@itemprop='nationality']")?.InnerText.Trim()
            };
        }
    }
}
