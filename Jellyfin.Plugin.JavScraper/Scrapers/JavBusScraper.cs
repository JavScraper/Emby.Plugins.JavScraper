using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Jellyfin.Plugin.JavScraper.Extensions;
using Jellyfin.Plugin.JavScraper.Http;
using Jellyfin.Plugin.JavScraper.Scrapers.Model;
using Jellyfin.Plugin.JavScraper.Services;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Scrapers
{
    /// <summary>
    /// https://www.javbus.com/BIJN-172
    /// </summary>
    public class JavBusScraper : AbstractScraper
    {
        private readonly IHttpClientManager _httpClientManager;

        public JavBusScraper(ILogger logger, IHttpClientManager httpClientManager, DMMService dmmService)
            : base("https://www.javbus.com/", logger, dmmService)
        {
            _httpClientManager = httpClientManager;
        }

        public override string Name => "JavBus";

        protected override bool IsKeyValid(string key)
            => JavIdRecognizer.FC2(key) == null;

        protected override async Task<IReadOnlyCollection<JavVideoIndex>> DoSearch(string key)
        {
            // https://www.javbus.com/search/33&type=1
            // https://www.javbus.com/uncensored/search/33&type=0&parent=uc
            var doc = await _httpClientManager.GetClient().GetHtmlDocumentAsync(new Uri(BaseAddress, $"/search/{key}&type=1&parent=ce"), true).ConfigureAwait(false);
            if (doc == null)
            {
                return Array.Empty<JavVideoIndex>();
            }

            var indexList = ParseIndex(doc);

            // 判断是否有 无码的影片
            var filmAmountText = doc.DocumentNode.SelectSingleNode("//a[contains(@href,'/uncensored/search/')]//span[contains(@class,'film')]/following::text()")?.InnerText ?? string.Empty;
            var match = Constants.RegexExpression.Number.Match(filmAmountText);
            if (match.Success && int.TryParse(match.Value, out var filmAmount))
            {
                if (filmAmount == 0)
                {
                    return indexList;
                }

                doc = await _httpClientManager.GetClient().GetHtmlDocumentAsync(new Uri(BaseAddress, $"/uncensored/search/{key}&type=1")).ConfigureAwait(false);
                if (doc == null)
                {
                    return indexList;
                }

                return indexList.Union(ParseIndex(doc)).ToArray();
            }

            return ParseIndex(doc);
        }

        private IReadOnlyCollection<JavVideoIndex> ParseIndex(HtmlDocument doc)
        {
            var indexList = new List<JavVideoIndex>();
            var nodes = doc.DocumentNode.SelectNodes("//a[@class='movie-box']");
            if (nodes?.Any() != true)
            {
                return indexList;
            }

            foreach (var node in nodes)
            {
                var url = node.GetAttributeValue("href", null);
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                var index = new JavVideoIndex() { Provider = Name, Url = url };

                var img = node.SelectSingleNode(".//div[@class='photo-frame']//img");
                if (img != null)
                {
                    index.Cover = img.GetAttributeValue("src", null);
                    index.Title = img.GetAttributeValue("title", null);
                }

                var dates = node.SelectNodes(".//date");
                if (dates?.Count >= 1)
                {
                    index.Num = dates[0].InnerText.Trim();
                }

                if (dates?.Count >= 2)
                {
                    index.Date = dates[1].InnerText.Trim();
                }

                if (string.IsNullOrWhiteSpace(index.Num))
                {
                    continue;
                }

                indexList.Add(index);
            }

            return indexList;
        }

        protected override async Task<JavVideo?> DoGetJavVideo(JavVideoIndex index)
        {
            // https://www.javbus.com/ABP-933
            var doc = await _httpClientManager.GetClient().GetHtmlDocumentAsync(index.Url).ConfigureAwait(false);
            if (doc == null)
            {
                return null;
            }

            var node = doc.DocumentNode.SelectSingleNode("//div[@class='container']/h3/..");
            if (node == null)
            {
                return null;
            }

            var nodes = node.SelectNodes(".//span[@class='header']");
            if (nodes == null || !nodes.Any())
            {
                return null;
            }

            var dic = new Dictionary<string, string>();
            foreach (var n in nodes)
            {
                var next = n.NextSibling;
                while (next != null && string.IsNullOrWhiteSpace(next.InnerText))
                {
                    next = next.NextSibling;
                }

                if (next != null)
                {
                    dic[n.InnerText.Trim()] = next.InnerText.Trim();
                }
            }

            return new JavVideo()
            {
                Provider = Name,
                Url = index.Url,
                Title = node.SelectSingleNode("./h3")?.InnerText?.Trim() ?? string.Empty,
                Cover = node.SelectSingleNode(".//a[@class='bigImage']")?.GetAttributeValue("href", null) ?? string.Empty,
                Num = dic.FirstOrDefault(item => item.Key.Contains("識別碼", StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty,
                Date = dic.FirstOrDefault(item => item.Key.Contains("發行日期", StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty,
                Runtime = dic.FirstOrDefault(item => item.Key.Contains("長度", StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty,
                Maker = dic.FirstOrDefault(item => item.Key.Contains("發行商", StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty,
                Studio = dic.FirstOrDefault(item => item.Key.Contains("製作商", StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty,
                Set = dic.FirstOrDefault(item => item.Key.Contains("系列", StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty,
                Director = dic.FirstOrDefault(item => item.Key.Contains("導演", StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty,
                // Plot = node.SelectSingleNode("./h3")?.InnerText,
                Genres = node.SelectNodes(".//span[@class='genre']/label")?.Select(o => o.InnerText.Trim()).ToArray() ?? Array.Empty<string>(),
                Actors = node.SelectNodes(".//div[@class='star-name']")?.Select(o => o.InnerText.Trim()).ToArray() ?? Array.Empty<string>(),
                Samples = doc.DocumentNode.SelectNodes("//a[@class='sample-box']//img")?.Select(o => o.GetAttributeValue("src", string.Empty).Trim()).ToArray() ?? Array.Empty<string>(),
            };
        }
    }
}
