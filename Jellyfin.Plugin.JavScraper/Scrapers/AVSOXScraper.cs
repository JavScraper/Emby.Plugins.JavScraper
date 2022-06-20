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
    public class AVSOXScraper : AbstractScraper
    {
        private readonly IHttpClientManager _httpClientManager;

        public AVSOXScraper(ILoggerFactory loggerFactory, IHttpClientManager httpClientManager, DMMService dmmService)
            : base("https://avsox.monster", loggerFactory.CreateLogger<AVSOXScraper>(), dmmService)
        {
            _httpClientManager = httpClientManager;
        }

        public override string Name => "AVSOX";

        protected override bool IsKeyValid(string key) => true;

        protected override async Task<IReadOnlyCollection<JavVideoIndex>> DoSearch(string key)
        {
            // https://avsox.monster/cn/search/032416_525
            var doc = await _httpClientManager.GetClient().GetHtmlDocumentAsync(new Uri(BaseAddress, $"/cn/search/{key}")).ConfigureAwait(false);
            if (doc == null)
            {
                return Array.Empty<JavVideoIndex>();
            }

            return ParseIndex(doc);
        }

        private IReadOnlyCollection<JavVideoIndex> ParseIndex(HtmlDocument doc)
        {
            var vedioIndexList = new List<JavVideoIndex>();
            if (doc == null)
            {
                return vedioIndexList;
            }

            var nodes = doc.DocumentNode.SelectNodes("//div[@class='item']//a[contains(@class, 'movie')]");
            if (nodes == null)
            {
                return vedioIndexList;
            }

            foreach (var node in nodes)
            {
                var url = node.GetAttributeValue("href", string.Empty);
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                var vedioIndex = new JavVideoIndex() { Provider = Name, Url = url };

                var img = node.SelectSingleNode(".//div[@class='photo-frame']//img");
                if (img != null)
                {
                    vedioIndex.Cover = img.GetAttributeValue("src", string.Empty);
                    vedioIndex.Title = img.GetAttributeValue("title", string.Empty);
                }

                var dates = node.SelectNodes(".//date");
                if (dates.Count >= 1)
                {
                    vedioIndex.Num = dates[0].InnerText.Trim();
                }

                if (dates.Count >= 2)
                {
                    vedioIndex.Date = dates[1].InnerText.Trim();
                }

                if (string.IsNullOrWhiteSpace(vedioIndex.Num))
                {
                    continue;
                }

                vedioIndexList.Add(vedioIndex);
            }

            return vedioIndexList;
        }

        protected override async Task<JavVideo?> DoGetJavVideo(JavVideoIndex index)
        {
            // https://www.javbus.cloud/ABP-933
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

            var dic = new Dictionary<string, string>();
            var nodes = node.SelectNodes(".//*[@class='header']");
            if (nodes == null)
            {
                return null;
            }

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
                Title = node.SelectSingleNode("./h3")?.InnerText.Trim() ?? string.Empty,
                Cover = node.SelectSingleNode(".//a[@class='bigImage']")?.GetAttributeValue("href", null) ?? string.Empty,
                Num = dic.GetValueOrDefault("识别码:", string.Empty),
                Date = dic.GetValueOrDefault("发行时间:", string.Empty),
                Runtime = dic.GetValueOrDefault("长度:", string.Empty),
                Maker = dic.GetValueOrDefault("制作商:", string.Empty),
                Studio = dic.GetValueOrDefault("制作商:", string.Empty),
                Genres = node.SelectNodes(".//span[@class='genre']").Select(o => o.InnerText.Trim()).ToList(),
                Actors = node.SelectNodes(".//*[@class='avatar-box']").Select(o => o.InnerText.Trim()).ToList(),
            };
        }
    }
}
