using Emby.Plugins.JavScraper.Http;
using HtmlAgilityPack;
#if __JELLYFIN__
using Microsoft.Extensions.Logging;
#else
using MediaBrowser.Model.Logging;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper.Scrapers
{
    /// <summary>
    /// https://www.javbus.com/BIJN-172
    /// </summary>
    public class JavBus : AbstractScraper
    {
        /// <summary>
        /// 适配器名称
        /// </summary>
        public override string Name => "JavBus";

        /// <summary>
        /// 构造
        /// </summary>
        /// <param name="handler"></param>
        public JavBus(
#if __JELLYFIN__
            ILoggerFactory logManager
#else
            ILogManager logManager
#endif
            )
            : base("https://www.javbus.com/", logManager.CreateLogger<JavBus>())
        {
        }

        /// <summary>
        /// 检查关键字是否符合
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override bool CheckKey(string key)
            => JavIdRecognizer.FC2(key) == null;

        /// <summary>
        /// 获取列表
        /// </summary>
        /// <param name="key">关键字</param>
        /// <returns></returns>
        protected override async Task<List<JavVideoIndex>> DoQyery(List<JavVideoIndex> ls, string key)
        {
            //https://www.javbus.cloud/search/33&type=1
            //https://www.javbus.cloud/uncensored/search/33&type=0&parent=uc
            var doc = await GetHtmlDocumentAsync($"/search/{key}&type=1");
            if (doc != null)
            {
                ParseIndex(ls, doc);

                //判断是否有 无码的影片
                var node = doc.DocumentNode.SelectSingleNode("//a[contains(@href,'/uncensored/search/')]");
                if (node != null)
                {
                    var t = node.InnerText;
                    var ii = t.Split('/');
                    //没有
                    if (ii.Length > 2 && ii[1].Trim().StartsWith("0"))
                        return ls;
                }
            }
            doc = await GetHtmlDocumentAsync($"/uncensored/search/{key}&type=1");
            ParseIndex(ls, doc);

            SortIndex(key, ls);
            return ls;
        }

        /// <summary>
        /// 解析列表
        /// </summary>
        /// <param name="ls"></param>
        /// <param name="doc"></param>
        /// <returns></returns>
        protected override List<JavVideoIndex> ParseIndex(List<JavVideoIndex> ls, HtmlDocument doc)
        {
            if (doc == null)
                return ls;
            var nodes = doc.DocumentNode.SelectNodes("//a[@class='movie-box']");
            if (nodes?.Any() != true)
                return ls;

            foreach (var node in nodes)
            {
                var url = node.GetAttributeValue("href", null);
                if (string.IsNullOrWhiteSpace(url))
                    continue;
                var m = new JavVideoIndex() { Provider = Name, Url = url };

                var img = node.SelectSingleNode(".//div[@class='photo-frame']//img");
                if (img != null)
                {
                    m.Cover = img.GetAttributeValue("src", null);
                    m.Title = img.GetAttributeValue("title", null);
                }
                var dates = node.SelectNodes(".//date");
                if (dates?.Count >= 1)
                    m.Num = dates[0].InnerText.Trim();
                if (dates?.Count >= 2)
                    m.Date = dates[1].InnerText.Trim();

                if (string.IsNullOrWhiteSpace(m.Num))
                    continue;
                ls.Add(m);

            }

            return ls;
        }

        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="url">地址</param>
        /// <returns></returns>
        public override async Task<JavVideo> Get(string url)
        {
            //https://www.javbus.cloud/ABP-933
            var doc = await GetHtmlDocumentAsync(url);
            if (doc == null)
                return null;

            var node = doc.DocumentNode.SelectSingleNode("//div[@class='container']/h3/..");
            if (node == null)
                return null;

            var dic = new Dictionary<string, string>();
            var nodes = node.SelectNodes(".//span[@class='header']");
            foreach (var n in nodes)
            {
                var next = n.NextSibling;
                while (next != null && string.IsNullOrWhiteSpace(next.InnerText))
                    next = next.NextSibling;
                if (next != null)
                    dic[n.InnerText.Trim()] = next.InnerText.Trim();
            }

            string GetValue(string _key)
                => dic.Where(o => o.Key.Contains(_key)).Select(o => o.Value).FirstOrDefault();

            var genres = node.SelectNodes(".//span[@class='genre']")?
                 .Select(o => o.InnerText.Trim()).ToList();

            var actors = node.SelectNodes(".//div[@class='star-name']")?
                 .Select(o => o.InnerText.Trim()).ToList();

            var samples = node.SelectNodes(".//a[@class='sample-box']")?
                 .Select(o => o.GetAttributeValue("href", null)).Where(o => o != null).ToList();
            var m = new JavVideo()
            {
                Provider = Name,
                Url = url,
                Title = node.SelectSingleNode("./h3")?.InnerText?.Trim(),
                Cover = node.SelectSingleNode(".//a[@class='bigImage']")?.GetAttributeValue("href", null),
                Num = GetValue("識別碼"),
                Date = GetValue("發行日期"),
                Runtime = GetValue("長度"),
                Maker = GetValue("發行商"),
                Studio = GetValue("製作商"),
                Set = GetValue("系列"),
                Director = GetValue("導演"),
                //Plot = node.SelectSingleNode("./h3")?.InnerText,
                Genres = genres,
                Actors = actors,
                Samples = samples,
            };

            m.Plot = await GetDmmPlot(m.Num);
            //去除标题中的番号
            if (string.IsNullOrWhiteSpace(m.Num) == false && m.Title?.StartsWith(m.Num, StringComparison.OrdinalIgnoreCase) == true)
                m.Title = m.Title.Substring(m.Num.Length).Trim();

            return m;
        }
    }
}