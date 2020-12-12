using HtmlAgilityPack;

#if __JELLYFIN__
using Microsoft.Extensions.Logging;
#else

using MediaBrowser.Model.Logging;

#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Emby.Plugins.JavScraper.Scrapers
{
    /// <summary>
    /// https://www.r18.com/videos/vod/movies/detail/-/id=118abw00032/?i3_ref=search&i3_ord=1
    /// </summary>
    public class R18 : AbstractScraper
    {
        /// <summary>
        /// 适配器名称
        /// </summary>
        public override string Name => "R18";

        /// <summary>
        /// 构造
        /// </summary>
        /// <param name="handler"></param>
        public R18(ILogger log = null)
            : base("https://www.r18.com/", log)
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
            //https://www.r18.com/common/search/searchword=ABW-032/
            var doc = await GetHtmlDocumentAsync($"/common/search/searchword={key}/?lg=zh");
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
            var nodes = doc.DocumentNode.SelectNodes("//li[@class='item-list']");
            if (nodes?.Any() != true)
                return ls;

            foreach (var node in nodes)
            {
                var title_node = node.SelectSingleNode("./a");
                if (title_node == null)
                    continue;
                var url = title_node.GetAttributeValue("href", null);
                if (string.IsNullOrWhiteSpace(url))
                    continue;
                var img = title_node.SelectSingleNode(".//img");
                if (img == null)
                    continue;
                var t2 = title_node.SelectSingleNode(".//dt");
                var m = new JavVideoIndex()
                {
                    Provider = Name,
                    Url = url + "&lg=zh",
                    Num = img.GetAttributeValue("alt", null),
                    Title = t2?.InnerText.Trim(),
                    Cover = img.GetAttributeValue("src", null),
                };
                if (string.IsNullOrWhiteSpace(m.Title))
                    m.Title = m.Num;

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
            //https://www.r18.com/videos/vod/movies/detail/-/id=ssni00879/?dmmref=video.movies.popular&i3_ref=list&i3_ord=4
            var doc = await GetHtmlDocumentAsync(url);
            if (doc == null)
                return null;

            var node = doc.DocumentNode.SelectSingleNode("//div[@class='product-details-page']");
            if (node == null)
                return null;

            var product_details = node.SelectSingleNode(".//div[@class='product-details']");

            string GetValueByItemprop(string name)
                => product_details.SelectSingleNode($".//dd[@itemprop='{name}']")?.InnerText.Trim().Trim('-');

            string GetDuration()
            {
                var _d = GetValueByItemprop("duration");
                if (string.IsNullOrWhiteSpace(_d))
                    return null;
                var _m = Regex.Match(_d, @"[\d]+");
                if (_m.Success)
                    return _m.Value;
                return null;
            }
            var dic = new Dictionary<string, string>();
            var nodes = product_details.SelectNodes(".//dt");
            foreach (var n in nodes)
            {
                var name = n.InnerText.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                //获取下一个标签
                var nx = n;
                do
                {
                    nx = nx.NextSibling;
                    if (nx == null || nx.Name == "dt")
                    {
                        nx = null;
                        break;
                    }
                    if (nx.Name == "dd")
                        break;
                } while (true);
                if (nx == null)
                    continue;

                var aa = nx.SelectNodes(".//a");
                var value = aa?.Any() == true ? string.Join(", ", aa.Select(o => o.InnerText.Trim()?.Trim('-')).Where(o => string.IsNullOrWhiteSpace(o) == false))
                    : nx?.InnerText?.Trim()?.Trim('-');

                if (string.IsNullOrWhiteSpace(value) == false)
                    dic[name] = value;
            }

            string GetValue(string _key)
                => dic.Where(o => o.Key.Contains(_key)).Select(o => o.Value).FirstOrDefault();

            var genres = product_details.SelectNodes(".//*[@itemprop='genre']")
                .Select(o => o.InnerText.Trim()?.Trim('-')).Where(o => string.IsNullOrWhiteSpace(o) == false).ToList();

            var actors = product_details.SelectNodes(".//div[@itemprop='actors']//*[@itemprop='name']")
                .Select(o => o.InnerText.Trim()?.Trim('-')).Where(o => string.IsNullOrWhiteSpace(o) == false).ToList();

            var product_gallery = doc.GetElementbyId("product-gallery");
            var samples = product_gallery.SelectNodes(".//img")?
                 .Select(o => o.GetAttributeValue("data-src", null) ?? o.GetAttributeValue("src", null)).Where(o => o != null).ToList();

            var m = new JavVideo()
            {
                Provider = Name,
                Url = url,
                Title = HttpUtility.HtmlDecode(node.SelectSingleNode(".//cite")?.InnerText?.Trim() ?? string.Empty),
                Cover = node.SelectSingleNode(".//img[@itemprop='image']")?.GetAttributeValue("src", null)?.Replace("ps.", "pl."),
                Num = GetValue("DVD ID:"),
                Date = GetValueByItemprop("dateCreated")?.Replace('/', '-'),
                Runtime = GetDuration(),
                Maker = GetValue("片商:"),
                Studio = GetValue("廠牌:"),
                Set = GetValue("系列:"),
                Director = GetValueByItemprop("director"),
                //Plot = node.SelectSingleNode("//p[@class='txt introduction']")?.InnerText,
                Genres = genres,
                Actors = actors,
                Samples = samples,
            };

            if (string.IsNullOrWhiteSpace(m.Title))
                m.Title = m.Num;

            if (string.IsNullOrWhiteSpace(m.Plot))
                m.Plot = await GetDmmPlot(m.Num);

            //去除标题中的番号
            if (string.IsNullOrWhiteSpace(m.Num) == false && m.Title?.StartsWith(m.Num, StringComparison.OrdinalIgnoreCase) == true)
                m.Title = m.Title.Substring(m.Num.Length).Trim();

            return m;
        }
    }
}