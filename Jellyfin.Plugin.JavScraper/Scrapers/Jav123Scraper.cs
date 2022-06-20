using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// https://www.jav321.com/
    /// </summary>
    public class Jav123Scraper : AbstractScraper
    {
        private readonly IHttpClientManager _httpClientManager;

        public Jav123Scraper(ILoggerFactory loggerFactory, IHttpClientManager httpClientManager, DMMService dmmService)
            : base("https://www.jav321.com", loggerFactory.CreateLogger<Jav123Scraper>(), dmmService)
        {
            _httpClientManager = httpClientManager;
        }

        public override string Name => "Jav123";

        protected override bool IsKeyValid(string key)
            => JavIdRecognizer.FC2(key) == null;

        protected override async Task<IReadOnlyCollection<JavVideoIndex>> DoSearch(string key)
        {
            // https://www.jav321.com/search
            // POST sn=key
            var doc = await _httpClientManager.GetClient().GetHtmlDocumentByPostFormAsync(new Uri(BaseAddress, $"/search"), new Dictionary<string, string>() { ["sn"] = key }).ConfigureAwait(false);
            if (doc == null)
            {
                return Array.Empty<JavVideoIndex>();
            }

            var video = ParseVideo(null, doc);
            if (video == null)
            {
                return Array.Empty<JavVideoIndex>();
            }

            return new List<JavVideoIndex> { video };
        }

        protected override async Task<JavVideo?> DoGetJavVideo(JavVideoIndex index)
        {
            // https://javdb.com/v/BzbA6
            var doc = await _httpClientManager.GetClient().GetHtmlDocumentAsync(index.Url).ConfigureAwait(false);
            if (doc == null)
            {
                return null;
            }

            return ParseVideo(index.Url, doc);
        }

        private JavVideo? ParseVideo(string? url, HtmlDocument doc)
        {
            var node = doc.DocumentNode.SelectSingleNode("//div[@class='panel-heading']/h3/../..");
            if (node == null)
            {
                return null;
            }

            var nodes = node.SelectNodes(".//b");
            if (nodes == null || !nodes.Any())
            {
                return null;
            }

            var dic = new Dictionary<string, string>();
            foreach (var n in nodes)
            {
                var name = n.InnerText.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (name.Contains("平均評価", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Constants.RegexExpression.Number.Match(n.SelectSingleNode("./following::img").GetAttributeValue("data-original", string.Empty));
                    if (match.Success)
                    {
                        dic[name] = match.Value;
                    }

                    continue;
                }

                var arr = new List<string>();

                var next = n.NextSibling;
                while (next != null && next.Name != "b")
                {
                    arr.Add(next.InnerText);
                    next = next.NextSibling;
                }

                if (!arr.Any())
                {
                    continue;
                }

                var value = string.Join(", ", arr.Select(o => o.Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase).Trim(": ".ToArray())).Where(o => !string.IsNullOrWhiteSpace(o)));

                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                dic[name] = value;
            }

            string GetCover()
            {
                var img = node.SelectSingleNode(".//*[@id='vjs_sample_player']")?.GetAttributeValue("poster", null);
                if (!string.IsNullOrWhiteSpace(img))
                {
                    return img;
                }

                img = node.SelectSingleNode(".//*[@id='video-player']")?.GetAttributeValue("poster", null);
                if (!string.IsNullOrWhiteSpace(img))
                {
                    return img;
                }

                img = doc.DocumentNode.SelectSingleNode("//img[@class='img-responsive']")?.GetAttributeValue("src", null);
                if (!string.IsNullOrWhiteSpace(img))
                {
                    return img;
                }

                return string.Empty;
            }

            List<string> GetSamples()
            {
                return doc.DocumentNode.SelectNodes("//a[contains(@href,'snapshot')]/img")
                      ?.Select(o => o.GetAttributeValue("src", null))
                      .Where(o => !string.IsNullOrWhiteSpace(o))
                      .ToList()
                      ?? new List<string>();
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                url = doc.DocumentNode.SelectSingleNode("//li/a[contains(text(),'简体中文')]")?.GetAttributeValue("href", null) ?? string.Empty;
                if (url.StartsWith("//", StringComparison.Ordinal))
                {
                    url = $"https:{url}";
                }
            }

            return new JavVideo()
            {
                Provider = Name,
                Url = url,
                Title = node.SelectSingleNode(".//h3/text()")?.InnerText?.Trim() ?? string.Empty,
                Cover = GetCover(),
                Num = dic.GetValueOrDefault("品番", string.Empty).ToUpper(CultureInfo.CurrentCulture),
                Date = dic.GetValueOrDefault("配信開始日", string.Empty),
                Runtime = dic.GetValueOrDefault("収録時間", string.Empty),
                Maker = dic.GetValueOrDefault("メーカー", string.Empty),
                Studio = dic.GetValueOrDefault("メーカー", string.Empty),
                Set = dic.GetValueOrDefault("シリーズ", string.Empty),
                Director = dic.GetValueOrDefault("导演", string.Empty),
                Genres = dic.GetValueOrDefault("ジャンル", string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).Distinct().ToList(),
                Actors = dic.GetValueOrDefault("出演者", string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).Distinct().ToList(),
                Samples = GetSamples(),
                Overview = node.SelectSingleNode("./div[@class='panel-body']/div[last()]")?.InnerText?.Trim() ?? string.Empty,
                CommunityRating = float.TryParse(dic.GetValueOrDefault("平均評価", string.Empty), out var rating) ? rating / 50 * 10 : null
            };
        }
    }
}
