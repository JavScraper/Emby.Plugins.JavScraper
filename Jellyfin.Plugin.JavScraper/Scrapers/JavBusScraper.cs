using System;
using System.Collections.Generic;
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
    /// https://www.javbus.com/BIJN-172
    /// </summary>
    public class JavBusScraper : AbstractScraper
    {
        /// <summary>
        /// 构造
        /// </summary>
        public JavBusScraper(ILoggerFactory loggerFactory, ApplicationDbContext applicationDbContext)
            : base("https://www.javbus.com/", loggerFactory.CreateLogger<JavBusScraper>(), applicationDbContext)
        {
        }

        /// <summary>
        /// 适配器名称
        /// </summary>
        public override string Name => "JavBus";

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
            // https://www.javbus.cloud/search/33&type=1
            // https://www.javbus.cloud/uncensored/search/33&type=0&parent=uc
            var doc = await GetHtmlDocumentAsync($"/search/{key}&type=1").ConfigureAwait(false);
            if (doc != null)
            {
                var indexList = ParseIndex(doc);

                // 判断是否有 无码的影片
                var node = doc.DocumentNode.SelectSingleNode("//a[contains(@href,'/uncensored/search/')]");
                if (node != null)
                {
                    var ii = node.InnerText.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    // 没有
                    if (ii.Length > 2 && ii[1].Trim().StartsWith("0", StringComparison.Ordinal))
                    {
                        return indexList;
                    }
                }
            }

            doc = await GetHtmlDocumentAsync($"/uncensored/search/{key}&type=1").ConfigureAwait(false);
            if (doc != null)
            {
                return SortIndex(key, ParseIndex(doc));
            }

            return new List<JavVideoIndex>(0);
        }

        /// <summary>
        /// 解析列表
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        protected override List<JavVideoIndex> ParseIndex(HtmlDocument doc)
        {
            var indexList = new List<JavVideoIndex>();
            if (doc == null)
            {
                return indexList;
            }

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

        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="url">地址</param>
        /// <returns></returns>
        protected override async Task<JavVideo?> GetJavVedio(string url)
        {
            // https://www.javbus.cloud/ABP-933
            var doc = await GetHtmlDocumentAsync(url).ConfigureAwait(false);
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

            var vedio = new JavVideo()
            {
                Provider = Name,
                Url = url,
                Title = node.SelectSingleNode("./h3")?.InnerText?.Trim() ?? string.Empty,
                Cover = node.SelectSingleNode(".//a[@class='bigImage']")?.GetAttributeValue("href", null) ?? string.Empty,
                Num = dic.GetValueOrDefault("識別碼", string.Empty),
                Date = dic.GetValueOrDefault("發行日期", string.Empty),
                Runtime = dic.GetValueOrDefault("長度", string.Empty),
                Maker = dic.GetValueOrDefault("發行商", string.Empty),
                Studio = dic.GetValueOrDefault("製作商", string.Empty),
                Set = dic.GetValueOrDefault("系列", string.Empty),
                Director = dic.GetValueOrDefault("導演", string.Empty),
                // Plot = node.SelectSingleNode("./h3")?.InnerText,
                Genres = node.SelectNodes(".//span[@class='genre']")?.Select(o => o.InnerText.Trim()).ToList() ?? new List<string>(0),
                Actors = node.SelectNodes(".//div[@class='star-name']")?.Select(o => o.InnerText.Trim()).ToList() ?? new List<string>(0),
                Samples = node.SelectNodes(".//a[@class='sample-box']")?.Select(o => o.InnerText.Trim()).ToList() ?? new List<string>(0),
            };

            vedio.Overview = await GetDmmPlot(vedio.Num).ConfigureAwait(false) ?? string.Empty;
            // 去除标题中的番号
            if (!string.IsNullOrWhiteSpace(vedio.Num) && vedio.Title.StartsWith(vedio.Num, StringComparison.OrdinalIgnoreCase))
            {
                vedio.Title = vedio.Title.Substring(vedio.Num.Length).Trim();
            }

            return vedio;
        }
    }
}
