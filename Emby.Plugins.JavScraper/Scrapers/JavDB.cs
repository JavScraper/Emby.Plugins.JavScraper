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
    public class JavDB : AbstractScraper
    {
        /// <summary>
        /// 适配器名称
        /// </summary>
        public override string Name => "JavDB";

        /// <summary>
        /// 构造
        /// </summary>
        /// <param name="handler"></param>
        public JavDB(ILogger log = null)
            : base("https://javdb.com/", log)
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
            ///https://javdb.com/search?q=ADN-106&f=all
            var doc = await GetHtmlDocumentAsync($"/search?q={key}&f=all");
            if (doc != null)
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
            var nodes = doc.DocumentNode.SelectNodes("//*[@id='videos']/div/div/a");
            if (nodes?.Any() != true)
                return ls;

            foreach (var node in nodes)
            {
                var url = node.GetAttributeValue("href", null);
                if (string.IsNullOrWhiteSpace(url))
                    continue;
                var m = new JavVideoIndex() { Provider = Name, Url = new Uri(client.BaseAddress, url).ToString() };
                ls.Add(m);
                var img = node.SelectSingleNode("./div/img");
                if (img != null)
                {
                    m.Cover = img.GetAttributeValue("src", null);
                    if (m.Cover?.StartsWith("//") == true)
                        m.Cover = $"https:{m.Cover}";
                }

                m.Num = node.SelectSingleNode("./div[@class='uid']")?.InnerText.Trim();
                m.Title = node.SelectSingleNode("./div[@class='video-title']")?.InnerText.Trim();
                m.Date = node.SelectSingleNode("./div[@class='meta']")?.InnerText.Trim();

                if (string.IsNullOrWhiteSpace(m.Num) == false && m.Title?.StartsWith(m.Num, StringComparison.OrdinalIgnoreCase) == true)
                    m.Title = m.Title.Substring(m.Num.Length).Trim();
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
            //https://javdb.com/v/BzbA6
            var doc = await GetHtmlDocumentAsync(url);
            if (doc == null)
                return null;

            var nodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'panel-block')]");
            if (nodes?.Any() != true)
                return null;

            var dic = new Dictionary<string, string>();
            foreach (var n in nodes)
            {
                var k = n.SelectSingleNode("./strong")?.InnerText?.Trim();
                string v = null;
                if (k?.Contains("演員") == true)
                {
                    var ac = n.SelectNodes("./*[@class='value']/a");
                    if (ac?.Any() == true)
                        v = string.Join(",", ac.Select(o => o.InnerText?.Trim()));
                }

                if (v == null)
                    v = n.SelectSingleNode("./*[@class='value']")?.InnerText?.Trim().Replace("&nbsp;", " ");

                if (string.IsNullOrWhiteSpace(k) == false && string.IsNullOrWhiteSpace(v) == false)
                    dic[k] = v;
            }

            string GetValue(string _key)
                => dic.Where(o => o.Key.Contains(_key)).Select(o => o.Value).FirstOrDefault();

            string GetCover()
            {
                var img = doc.DocumentNode.SelectSingleNode("//img[@class='box video-cover']")?.GetAttributeValue("src", null);
                if (string.IsNullOrWhiteSpace(img) == false)
                    return img;
                img = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']")?.GetAttributeValue("content", null);
                if (string.IsNullOrWhiteSpace(img) == false)
                    return img;
                img = doc.DocumentNode.SelectSingleNode("//meta[@class='fancybox-video']")?.GetAttributeValue("poster", null);

                return img;
            }

            List<string> GetGenres()
            {
                var v = GetValue("类别");
                if (string.IsNullOrWhiteSpace(v))
                    return null;
                return v.Split(',').Select(o => o.Trim()).Distinct().ToList();
            }

            List<string> GetActors()
            {
                var v = GetValue("演員");
                if (string.IsNullOrWhiteSpace(v))
                    return null;
                var ac = v.Split(',').Select(o => o.Trim()).Distinct().ToList();
                for (int i = 0; i < ac.Count; i++)
                {
                    var a = ac[i];
                    if (a.Contains("(") == false)
                        continue;
                    var arr = a.Split("()".ToArray(), StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
                    if (arr.Length == 2)
                        ac[i] = arr[1];
                }
                return ac;
            }

            List<string> GetSamples()
            {
                return doc.DocumentNode.SelectNodes("//div[@class='tile-images preview-images']/a")
                      ?.Select(o => o.GetAttributeValue("href", null))
                      .Where(o => string.IsNullOrWhiteSpace(o) == false).ToList();
            }

            var m = new JavVideo()
            {
                Provider = Name,
                Url = url,
                Title = doc.DocumentNode.SelectSingleNode("//*[contains(@class,'title')]/strong")?.InnerText?.Trim(),
                Cover = GetCover(),
                Num = GetValue("番號"),
                Date = GetValue("時間"),
                Runtime = GetValue("時長"),
                Maker = GetValue("片商"),
                Studio = GetValue("發行"),
                Set = GetValue("系列"),
                Director = GetValue("導演"),
                Genres = GetGenres(),
                Actors = GetActors(),
                Samples = GetSamples(),
            };

            m.Plot = await GetDmmPlot(m.Num);
            ////去除标题中的番号
            if (string.IsNullOrWhiteSpace(m.Num) == false && m.Title?.StartsWith(m.Num, StringComparison.OrdinalIgnoreCase) == true)
                m.Title = m.Title.Substring(m.Num.Length).Trim();

            return m;
        }
    }
}