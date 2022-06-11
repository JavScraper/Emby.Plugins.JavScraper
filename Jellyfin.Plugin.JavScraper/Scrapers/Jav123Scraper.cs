using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Jellyfin.Plugin.JavScraper.Data;
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
        /// <summary>
        /// 构造
        /// </summary>
        public Jav123Scraper(ILoggerFactory loggerFactory, ApplicationDbContext applicationDbContext, IHttpClientFactory clientFactory)
            : base("https://www.jav321.com", loggerFactory.CreateLogger<Jav123Scraper>(), applicationDbContext, clientFactory)
        {
        }

        /// <summary>
        /// 适配器名称
        /// </summary>
        public override string Name => "Jav123";

        /// <summary>
        /// 检查关键字是否符合
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        protected override bool CheckKey(string key)
            => JavIdRecognizer.FC2(key) == null;

        /// <summary>
        /// 获取列表
        /// </summary>
        /// <param name="key">关键字</param>
        /// <returns></returns>
        protected override async Task<IReadOnlyList<JavVideoIndex>> DoQyery(string key)
        {
            // https://www.jav321.com/search
            // POST sn=key
            var result = new List<JavVideoIndex>();
            var doc = await GetHtmlDocumentByPostAsync($"/search", new Dictionary<string, string>() { ["sn"] = key }).ConfigureAwait(false);
            if (doc != null)
            {
                var video = await ParseVideo(null, doc).ConfigureAwait(false);
                if (video != null)
                {
                    result.Add(video);
                }
            }

            return SortIndex(key, result);
        }

        /// <summary>
        /// 不用了
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        protected override List<JavVideoIndex> ParseIndex(HtmlDocument doc)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="url">地址</param>
        /// <returns></returns>
        protected override async Task<JavVideo?> GetJavVedio(string url)
        {
            // https://javdb.com/v/BzbA6
            var doc = await GetHtmlDocumentAsync(url).ConfigureAwait(false);
            if (doc == null)
            {
                return null;
            }

            return await ParseVideo(url, doc).ConfigureAwait(false);
        }

        private async Task<JavVideo?> ParseVideo(string? url, HtmlDocument doc)
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

            if (string.IsNullOrWhiteSpace(url))
            {
                url = doc.DocumentNode.SelectSingleNode("//li/a[contains(text(),'简体中文')]")?.GetAttributeValue("href", null) ?? string.Empty;
                if (url.StartsWith("//", StringComparison.Ordinal))
                {
                    url = $"https:{url}";
                }
            }

            var dic = new Dictionary<string, string>();
            foreach (var n in nodes)
            {
                var name = n.InnerText.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var arr = new List<string>();

                var next = n.NextSibling;
                while (next != null && next.Name != "b")
                {
                    arr.Add(next.InnerText);
                    next = next.NextSibling;
                }

                if (arr.Count == 0)
                {
                    continue;
                }

                var value = string.Join(", ", arr.Select(o => o.Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase).Trim(": ".ToArray())).Where(o => string.IsNullOrWhiteSpace(o) == false));

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

            var vedio = new JavVideo()
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
            };
            if (string.IsNullOrWhiteSpace(vedio.Overview))
            {
                vedio.Overview = await GetDmmPlot(vedio.Num).ConfigureAwait(false) ?? string.Empty;
            }
            ////去除标题中的番号
            if (string.IsNullOrWhiteSpace(vedio.Num) == false && vedio.Title?.StartsWith(vedio.Num, StringComparison.OrdinalIgnoreCase) == true)
            {
                vedio.Title = vedio.Title.Substring(vedio.Num.Length).Trim();
            }

            return vedio;
        }
    }
}
