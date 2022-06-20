using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Jellyfin.Plugin.JavScraper.Data;
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
    public class JavDBScraper : AbstractScraper
    {
        /// <summary>
        /// 番号分段识别
        /// </summary>
        private static readonly Regex _idRegex = new("((?<a>[a-z]{2,})|(?<b>[0-9]{2,}))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _ratingRegex = new(@"(?<rating>[\d.]+)分", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly IHttpClientManager _httpClientManager;

        public JavDBScraper(ILoggerFactory loggerFactory, IHttpClientManager httpClientManager, DMMService dmmService)
            : base("https://javdb.com/", loggerFactory.CreateLogger<JavDBScraper>(), dmmService)
        {
            _httpClientManager = httpClientManager;
        }

        public override string Name => "JavDB";

        protected override bool IsKeyValid(string key)
            => JavIdRecognizer.FC2(key) == null;

        protected override async Task<IReadOnlyCollection<JavVideoIndex>> DoSearch(string key)
        {
            // https://javdb.com/search?q=ADN-106&f=all
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseAddress, $"/search?q={key}&f=all"));
            request.Headers.Add("Cookie", "over18=1");
            var doc = await _httpClientManager.GetClient().SendAndReturnHtmlDocumentAsync(request).ConfigureAwait(false);
            if (doc == null)
            {
                return Array.Empty<JavVideoIndex>();
            }

            var keySegments = _idRegex.Matches(key).Cast<Match>().Select(o => o.Groups[0].Value.TrimStart('0')).ToList();
            return ParseIndex(doc).Where(index => keySegments.All(keySegment => index.Num.Contains(keySegment, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        private IReadOnlyList<JavVideoIndex> ParseIndex(HtmlDocument doc)
        {
            var indexList = new List<JavVideoIndex>();

            var nodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'movie')]//div[contains(@class, 'item')]");
            if (nodes == null || !nodes.Any())
            {
                return indexList;
            }

            return nodes.Where(node => !string.IsNullOrEmpty(node.SelectSingleNode("//a[@class='box']")?.GetAttributeValue("href", null)))
                .Select(node => new JavVideoIndex
                {
                    Provider = Name,
                    Url = new Uri(BaseAddress, node.SelectSingleNode("//a[@class='box']")?.GetAttributeValue("href", null)).ToString(),
                    Cover = node.SelectSingleNode("//div[contains(@class, 'cover')]//img")?.GetAttributeValue("src", null)?.Trim() ?? string.Empty,
                    Num = node.SelectSingleNode("//div[contains(@class, 'video-title')]/strong")?.InnerText?.Trim() ?? string.Empty,
                    Title = node.SelectSingleNode("//div[contains(@class, 'video-title')]/strong/following-sibling::text()")?.InnerText?.Trim() ?? string.Empty,
                    Date = node.SelectSingleNode("//div[contains(@class, 'meta')]")?.InnerText?.Trim() ?? string.Empty
                })
                .ToArray();
        }

        protected override async Task<JavVideo?> DoGetJavVideo(JavVideoIndex index)
        {
            // https://javdb.com/v/BzbA6
            var doc = await _httpClientManager.GetClient().GetHtmlDocumentAsync(index.Url).ConfigureAwait(false);
            if (doc == null)
            {
                return null;
            }

            var documentNode = doc.DocumentNode;
            var panelPath = "//nav[contains(@class, 'movie-panel-info')]";

            if (documentNode.SelectSingleNode(panelPath) == null)
            {
                return null;
            }

            return new JavVideo()
            {
                Provider = Name,
                Url = index.Url,
                Title = documentNode.SelectSingleNode("//*[contains(@class,'title')]/strong")?.InnerText.Trim() ?? string.Empty,
                Cover = documentNode.SelectSingleNode("//div[contains(@class, 'video-meta-panel')]//img[contains(@class, 'video-cover')]")?.GetAttributeValue("src", null)?.Trim() ?? string.Empty,
                Num = documentNode.SelectNodes($"{panelPath}//div//strong[contains(text(), '番號')]//following-sibling::span//text()")?.Select(textNode => textNode.InnerText.Trim()).Join() ?? string.Empty,
                Date = documentNode.SelectSingleNode($"{panelPath}//div//strong[contains(text(), '日期')]//following-sibling::span//text()")?.InnerText.Trim() ?? string.Empty,
                Runtime = documentNode.SelectSingleNode($"{panelPath}//div//strong[contains(text(), '時長')]//following-sibling::span//text()")?.InnerText.Trim() ?? string.Empty,
                Maker = documentNode.SelectSingleNode($"{panelPath}//div//strong[contains(text(), '片商')]//following-sibling::span//text()")?.InnerText.Trim() ?? string.Empty,
                Studio = documentNode.SelectSingleNode($"{panelPath}//div//strong[contains(text(), '片商')]//following-sibling::span//text()")?.InnerText.Trim() ?? string.Empty,
                Set = documentNode.SelectSingleNode($"{panelPath}//div//strong[contains(text(), '系列')]//following-sibling::span//text()")?.InnerText.Trim() ?? string.Empty,
                Director = documentNode.SelectSingleNode($"{panelPath}//div//strong[contains(text(), '導演')]//following-sibling::span//text()")?.InnerText.Trim() ?? string.Empty,
                Genres = documentNode.SelectNodes($"{panelPath}//div//strong[contains(text(), '類別')]//following-sibling::span//a//text()")?.Select(textNode => textNode.InnerText.Trim()).ToArray() ?? Array.Empty<string>(),
                Actors = documentNode.SelectNodes($"{panelPath}//div//strong[contains(text(), '演員')]//following-sibling::span//a//text()")?.Select(textNode => textNode.InnerText.Trim()).ToArray() ?? Array.Empty<string>(),
                Samples = documentNode.SelectNodes("//div[contains(@class, 'preview-images')]//a[contains(@class, 'tile-item')]//img")?.Select(imgNode => imgNode.GetAttributeValue("src", string.Empty).Trim()).Where(src => !string.IsNullOrEmpty(src)).ToArray() ?? Array.Empty<string>(),
                CommunityRating = documentNode.SelectSingleNode($"{panelPath}//div//strong[contains(text(), '評分')]//following-sibling::span//text()[last()]")?.InnerText.TryMatch(_ratingRegex, out var match) == true ? (float.TryParse(match.Groups["rating"].Value, out var rating) ? rating / 5.0f * 10f : 0) : 0,
            };
        }
    }
}
