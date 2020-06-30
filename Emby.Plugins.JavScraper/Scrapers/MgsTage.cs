using Emby.Plugins.JavScraper.Http;
using HtmlAgilityPack;
using MediaBrowser.Common.Net;
#if __JELLYFIN__
using Microsoft.Extensions.Logging;
#else
using MediaBrowser.Model.Logging;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper.Scrapers
{
    /// <summary>
    /// https://www.mgstage.com/product/product_detail/320MMGH-242/
    /// </summary>
    public class MgsTage : AbstractScraper
    {
        /// <summary>
        /// 适配器名称
        /// </summary>
        public override string Name => "MgsTage";

        private static Regex regexDate = new Regex(@"(?<date>[\d]{4}[-/][\d]{2}[-/][\d]{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// 构造
        /// </summary>
        /// <param name="handler"></param>
        public MgsTage(ILogger log = null)
            : base("https://www.mgstage.com/", log)
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
            //https://www.mgstage.com/search/search.php?search_word=320MMGH-242&disp_type=detail
            var doc = await GetHtmlDocumentAsync($"/search/search.php?search_word={key}&disp_type=detail");
            if (doc != null)
            {
                ParseIndex(ls, doc);
            }

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
            var nodes = doc.DocumentNode.SelectNodes("//div[@class='rank_list']/ul/li");
            if (nodes?.Any() != true)
                return ls;

            foreach (var node in nodes)
            {
                var title_node = node.SelectSingleNode("./h5/a");
                if (title_node == null)
                    continue;
                var url = title_node.GetAttributeValue("href", null);
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                var m = new JavVideoIndex()
                {
                    Provider = Name,
                    Url = new Uri(client.BaseAddress, url).ToString(),
                    Num = url.Split("/".ToArray(), StringSplitOptions.RemoveEmptyEntries).Last(),
                    Title = title_node.InnerText.Trim()
                };
                ls.Add(m);

                var img = node.SelectSingleNode("./h6/a/img");
                if (img != null)
                {
                    m.Cover = img.GetAttributeValue("src", null);
                }
                var date = node.SelectSingleNode(".//p[@class='data']");
                if (date != null)
                {
                    var d = date.InnerText.Trim();
                    var me = regexDate.Match(d);
                    if (me.Success)
                        m.Date = me.Groups["date"].Value.Replace('/', '-');
                }
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
            //https://www.mgstage.com/product/product_detail/320MMGH-242/
            var doc = await GetHtmlDocumentAsync(url);
            if (doc == null)
                return null;

            var node = doc.DocumentNode.SelectSingleNode("//div[@class='common_detail_cover']");
            if (node == null)
                return null;

            var dic = new Dictionary<string, string>();
            var nodes = node.SelectNodes(".//table/tr/th/..");
            foreach (var n in nodes)
            {
                var name = n.SelectSingleNode("./th")?.InnerText?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                //尝试获取 a 标签的内容
                var aa = n.SelectNodes("./td/a");
                var value = aa?.Any() == true ? string.Join(", ", aa.Select(o => o.InnerText.Trim()).Where(o => string.IsNullOrWhiteSpace(o) == false))
                    : n.SelectSingleNode("./td")?.InnerText?.Trim();

                if (string.IsNullOrWhiteSpace(value) == false)
                    dic[name] = value;
            }

            string GetValue(string _key)
                => dic.Where(o => o.Key.Contains(_key)).Select(o => o.Value).FirstOrDefault();

            var genres = GetValue("ジャンル")?.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList();

            var actors = GetValue("出演")?.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList();

            var samples = node.SelectNodes("//a[@class='sample_image']")?
                 .Select(o => o.GetAttributeValue("href", null)).Where(o => o != null).ToList();
            var m = new JavVideo()
            {
                Provider = Name,
                Url = url,
                Title = node.SelectSingleNode("./h1")?.InnerText?.Trim(),
                Cover = node.SelectSingleNode(".//img[@class='enlarge_image']")?.GetAttributeValue("src", null),
                Num = GetValue("品番"),
                Date = GetValue("配信開始日")?.Replace('/', '-'),
                Runtime = GetValue("収録時間"),
                Maker = GetValue("發行商"),
                Studio = GetValue("メーカー"),
                Set = GetValue("シリーズ"),
                Director = GetValue("シリーズ"),
                Plot = node.SelectSingleNode("//p[@class='txt introduction']")?.InnerText,
                Genres = genres,
                Actors = actors,
                Samples = samples,
            };

            if (string.IsNullOrWhiteSpace(m.Plot))
                m.Plot = await GetDmmPlot(m.Num);

            //去除标题中的番号
            if (string.IsNullOrWhiteSpace(m.Num) == false && m.Title?.StartsWith(m.Num, StringComparison.OrdinalIgnoreCase) == true)
                m.Title = m.Title.Substring(m.Num.Length).Trim();

            return m;
        }
    }
}