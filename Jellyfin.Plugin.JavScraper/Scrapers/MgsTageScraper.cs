using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
    /// https://www.mgstage.com/product/product_detail/320MMGH-242/
    /// </summary>
    public class MgsTageScraper : AbstractScraper
    {
        private readonly IHttpClientManager _httpClientManager;

        public MgsTageScraper(ILoggerFactory loggerFactory, IHttpClientManager httpClientManager, DMMService dmmService)
            : base("https://www.mgstage.com/", loggerFactory.CreateLogger<MgsTageScraper>(), dmmService)
        {
            _httpClientManager = httpClientManager;
        }

        public override string Name => "MgsTage";

        protected override bool IsKeyValid(string key)
            => JavIdRecognizer.FC2(key) == null;

        protected override async Task<IReadOnlyCollection<JavVideoIndex>> DoSearch(string key)
        {
            // https://www.mgstage.com/search/search.php?search_word=320MMGH-242&disp_type=detail
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseAddress, $"/search/search.php?search_word={key}&disp_type=detail"));
            request.Headers.Add("Cookie", "adc=1");
            var doc = await _httpClientManager.GetClient().SendAndReturnHtmlDocumentAsync(request).ConfigureAwait(false);
            if (doc == null)
            {
                return Array.Empty<JavVideoIndex>();
            }

            return ParseIndex(doc);
        }

        private IReadOnlyList<JavVideoIndex> ParseIndex(HtmlDocument doc)
        {
            var nodes = doc.DocumentNode.SelectNodes("//div[@class='rank_list']/ul/li");
            if (nodes == null)
            {
                return Array.Empty<JavVideoIndex>();
            }

            return nodes
                .Select(node =>
                {
                    var url = node.SelectSingleNode("./a")?.GetAttributeValue("href", null)?.Trim() ?? string.Empty;
                    return new JavVideoIndex()
                    {
                        Provider = Name,
                        Url = url,
                        Num = url.Split("/", StringSplitOptions.RemoveEmptyEntries).LastOrDefault(string.Empty),
                        Title = node.SelectSingleNode("//p[contains(@class, 'title')]//text()")?.InnerText?.Trim() ?? string.Empty,
                        Cover = node.SelectSingleNode("//h5//img")?.GetAttributeValue("src", null)?.Trim() ?? string.Empty
                    };
                })
                .ToArray();
        }

        protected override async Task<JavVideo?> DoGetJavVideo(JavVideoIndex index)
        {
            // https://www.mgstage.com/product/product_detail/320MMGH-242/
            using var request = new HttpRequestMessage(HttpMethod.Get, index.Url);
            request.Headers.Add("Cookie", "adc=1");
            var doc = await _httpClientManager.GetClient().SendAndReturnHtmlDocumentAsync(request).ConfigureAwait(false);
            if (doc == null)
            {
                return null;
            }

            var documentNode = doc.DocumentNode;

            return new JavVideo()
            {
                Provider = Name,
                Url = index.Url,
                Title = documentNode.SelectSingleNode("//h1[contains(@class, 'tag')]")?.InnerText.Trim() ?? string.Empty,
                Cover = documentNode.SelectSingleNode("//div[contains(@class, 'detail_photo')]//h2/img")?.GetAttributeValue("src", null)?.Trim() ?? string.Empty,
                Num = documentNode.SelectSingleNode("//th[(contains(text(), '品番'))]//following-sibling::td")?.InnerText.Trim() ?? string.Empty,
                Date = documentNode.SelectSingleNode("//th[(contains(text(), '商品発売日'))]//following-sibling::td")?.InnerText.Trim() ?? string.Empty,
                Runtime = documentNode.SelectSingleNode("//th[(contains(text(), '収録時間'))]//following-sibling::td")?.InnerText.Trim() ?? string.Empty,
                Maker = documentNode.SelectSingleNode("//th[(contains(text(), 'メーカー'))]//following-sibling::td//a//text()")?.InnerText.Trim() ?? string.Empty,
                Studio = documentNode.SelectSingleNode("//th[(contains(text(), 'メーカー'))]//following-sibling::td//a//text()")?.InnerText.Trim() ?? string.Empty,
                Set = documentNode.SelectSingleNode("//th[(contains(text(), 'シリーズ'))]//following-sibling::td//a//text()")?.InnerText.Trim() ?? string.Empty,
                Overview = documentNode.SelectSingleNode("//dl[contains(@id, 'introduction')]//p[contains(@class, 'introduction')]/text()")?.InnerText.Trim() ?? string.Empty,
                Genres = documentNode.SelectNodes("//th[(contains(text(), 'ジャンル'))]//following-sibling::td//a/text()")?.Select(textNode => textNode.InnerText.Trim()).ToArray() ?? Array.Empty<string>(),
                Actors = documentNode.SelectNodes("//th[(contains(text(), '出演'))]//following-sibling::td//a/text()")?.Select(textNode => textNode.InnerText.Trim()).ToArray() ?? Array.Empty<string>(),
                Samples = documentNode.SelectNodes("//dl[contains(@id, 'sample-photo')]//ul//img")?.Select(imgNode => imgNode.GetAttributeValue("src", string.Empty).Trim()).Where(src => !string.IsNullOrEmpty(src)).ToArray() ?? Array.Empty<string>(),
                CommunityRating = documentNode.SelectSingleNode("//th[(contains(text(), '評価'))]//following-sibling::td/span/following-sibling::text()")?.InnerText.TryMatch(Constants.RegexExpression.Float, out var match) == true ? (float.TryParse(match.Value, out var rating) ? rating / 5.0f * 10f : 0) : 0
            };
        }
    }
}
