using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Scrapers.Model;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Scrapers
{
    /// <summary>
    /// https://avsox.host/cn/search/032416_525
    /// https://avsox.host/cn/movie/77f594342b5e2afe
    /// </summary>
    public class AVSOXScraper : AbstractScraper
    {
        /// <summary>
        /// 构造
        /// </summary>
        public AVSOXScraper(ILoggerFactory loggerFactory, ApplicationDbContext applicationDbContext)
            : base("https://avsox.website/", loggerFactory.CreateLogger<AVSOXScraper>(), applicationDbContext)
        {
        }

        /// <summary>
        /// 适配器名称
        /// </summary>
        public override string Name => "AVSOX";

        /// <summary>
        /// 检查关键字是否符合
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        protected override bool CheckKey(string key) => true;

        /// <summary>
        /// 获取列表
        /// </summary>
        /// <param name="key">关键字</param>
        /// <returns></returns>
        protected override async Task<IReadOnlyList<JavVideoIndex>> DoQyery(string key)
        {
            // https://javdb.com/search?q=ADN-106&f=all
            var doc = await GetHtmlDocumentAsync($"/cn/search/{key}").ConfigureAwait(false);
            if (doc != null)
            {
                return SortIndex(key, ParseIndex(doc));
            }

            return new List<JavVideoIndex>();
        }

        /// <summary>
        /// 解析列表
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        protected override IReadOnlyCollection<JavVideoIndex> ParseIndex(HtmlDocument doc)
        {
            var vedioIndexList = new List<JavVideoIndex>();
            if (doc == null)
            {
                return vedioIndexList;
            }

            var nodes = doc.DocumentNode.SelectNodes("//div[@class='item']/a");
            if (!nodes.Any())
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

            var vedio = new JavVideo()
            {
                Provider = Name,
                Url = url,
                Title = node.SelectSingleNode("./h3")?.InnerText.Trim() ?? string.Empty,
                Cover = node.SelectSingleNode(".//a[@class='bigImage']")?.GetAttributeValue("href", null) ?? string.Empty,
                Num = dic.GetValueOrDefault("识别码", string.Empty),
                Date = dic.GetValueOrDefault("发行时间", string.Empty),
                Runtime = dic.GetValueOrDefault("长度", string.Empty),
                Maker = dic.GetValueOrDefault("发行商", string.Empty),
                Studio = dic.GetValueOrDefault("制作商", string.Empty),
                Set = dic.GetValueOrDefault("系列", string.Empty),
                Director = dic.GetValueOrDefault("导演", string.Empty),
                // Plot = node.SelectSingleNode("./h3")?.InnerText,
                Genres = node.SelectNodes(".//span[@class='genre']").Select(o => o.InnerText.Trim()).ToList(),
                Actors = node.SelectNodes(".//*[@class='avatar-box']").Select(o => o.InnerText.Trim()).ToList(),
                Samples = node.SelectNodes(".//a[@class='sample-box']").Select(o => o.GetAttributeValue("href", null)).Where(o => o != null).ToList(),
            };

            vedio.Overview = await GetDmmPlot(vedio.Num).ConfigureAwait(false) ?? string.Empty;
            // 去除标题中的番号
            if (!string.IsNullOrWhiteSpace(vedio.Num) && vedio.Title.StartsWith(vedio.Num, StringComparison.OrdinalIgnoreCase))
            {
                vedio.Title = vedio.Title[vedio.Num.Length..].Trim();
            }

            return vedio;
        }
    }
}
