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
    /// https://avsox.monster/cn/search/032416_525
    /// https://avsox.monster/cn/movie/77f594342b5e2afe
    /// </summary>
    public class AvsoxScraper : AbstractScraper
    {
        private readonly IHttpClientManager _httpClientManager;

        public AvsoxScraper(ILogger logger, IHttpClientManager httpClientManager, DMMService dmmService)
            : base("https://avsox.monster", logger, dmmService)
        {
            _httpClientManager = httpClientManager;
        }

        public override string Name => "AVSOX";

        protected override bool IsKeyValid(string key) => true;

        protected override async Task<IReadOnlyCollection<JavVideoIndex>> DoSearch(string key)
        {
            // https://avsox.monster/cn/search/032416_525
            var doc = await _httpClientManager.GetClient().GetHtmlDocumentAsync(new Uri(BaseAddress, $"/cn/search/{key}")).ConfigureAwait(false);
            return doc == null ? Array.Empty<JavVideoIndex>() : ParseIndex(doc);
        }

        private IReadOnlyList<JavVideoIndex> ParseIndex(HtmlDocument doc)
        {
            var nodes = doc.DocumentNode.SelectNodes("//div[@class='item']//a[contains(@class, 'movie')]");
            if (nodes == null)
            {
                return Array.Empty<JavVideoIndex>();
            }

            return nodes
                .Select(node =>
                {
                    var img = node.SelectSingleNode(".//div[@class='photo-frame']//img");
                    return new JavVideoIndex
                    {
                        Provider = Name,
                        Url = node.GetAttributeValue("href", string.Empty).Trim(),
                        Cover = img?.GetAttributeValue("src", null)?.Trim() ?? string.Empty,
                        Title = img?.GetAttributeValue("title", null)?.Trim() ?? string.Empty,
                        Num = node.SelectSingleNode(".//date[1]")?.InnerText.Trim() ?? string.Empty,
                        Date = node.SelectSingleNode(".//date[2]")?.InnerText.Trim() ?? string.Empty
                    };
                })
                .Where(index => !string.IsNullOrWhiteSpace(index.Url))
                .ToArray();
        }

        protected override async Task<JavVideo?> DoGetJavVideo(JavVideoIndex index)
        {
            // https://www.javbus.cloud/ABP-933
            var doc = await _httpClientManager.GetClient().GetHtmlDocumentAsync(index.Url).ConfigureAwait(false);
            if (doc == null)
            {
                return null;
            }

            return new JavVideo
            {
                Provider = Name,
                Url = index.Url,
                Title = doc.DocumentNode.SelectSingleNode("//h3")?.InnerText.Trim() ?? string.Empty,
                Cover = doc.DocumentNode.SelectSingleNode("//a[@class='bigImage']")?.GetAttributeValue("href", null)?.Trim() ?? string.Empty,
                Num = doc.DocumentNode.SelectSingleNode("//*[contains(text(), '识别码')]//following-sibling::*[1]")?.InnerText?.Trim() ?? string.Empty,
                Date = doc.DocumentNode.SelectSingleNode("//*[contains(text(), '发行时间')]//following-sibling::*[1]")?.InnerText?.Trim() ?? string.Empty,
                Runtime = doc.DocumentNode.SelectSingleNode("//*[contains(text(), '长度')]//following-sibling::*[1]")?.InnerText?.Trim() ?? string.Empty,
                Maker = doc.DocumentNode.SelectSingleNode("//*[contains(text(), '制作商')]//following-sibling::*[1]")?.InnerText?.Trim() ?? string.Empty,
                Studio = doc.DocumentNode.SelectSingleNode("//*[contains(text(), '制作商')]//following-sibling::*[1]")?.InnerText?.Trim() ?? string.Empty,
                Genres = doc.DocumentNode.SelectNodes("//span[@class='genre']")?.Select(o => o.InnerText.Trim()).ToArray() ?? Array.Empty<string>(),
                Actors = doc.DocumentNode.SelectNodes("//*[@class='avatar-box']")?.Select(o => o.InnerText.Trim()).ToArray() ?? Array.Empty<string>()
            };
        }
    }
}
