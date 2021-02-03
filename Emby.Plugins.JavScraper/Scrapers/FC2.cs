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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper.Scrapers
{
    /// <summary>
    /// https://fc2club.com/html/FC2-1249328.html
    /// </summary>
    public class FC2 : AbstractScraper
    {
        /// <summary>
        /// 适配器名称
        /// </summary>
        public override string Name => "FC2";

        private static Regex regexDate = new Regex(@"(?<date>[\d]{4}[-/][\d]{2}[-/][\d]{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static Regex regexFC2 = new Regex(@"FC2-*(PPV|)-(?<id>[\d]{2,10})($|[^\d])", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// 构造
        /// </summary>
        /// <param name="handler"></param>
        public FC2(
#if __JELLYFIN__
            ILoggerFactory logManager
#else
            ILogManager logManager
#endif
            )
            : base("https://fc2club.com/", logManager.CreateLogger<FC2>())
        {
        }

        /// <summary>
        /// 检查关键字是否符合
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override bool CheckKey(string key)
            => JavIdRecognizer.FC2(key) != null;

        public override Task<List<JavVideoIndex>> Query(string key)
        {
            var m = regexFC2.Match(key);
            if (m.Success == false)
                return Task.FromResult(new List<JavVideoIndex>());
            var id = m.Groups["id"].Value;
            return DoQyery(new List<JavVideoIndex>(), id);
        }

        /// <summary>
        /// 获取列表
        /// </summary>
        /// <param name="key">关键字</param>
        /// <returns></returns>
        protected override async Task<List<JavVideoIndex>> DoQyery(List<JavVideoIndex> ls, string key)
        {
            var item = await GetById(key);
            if (item != null)
            {
                ls.Add(new JavVideoIndex()
                {
                    Cover = item.Cover,
                    Date = item.Date,
                    Num = item.Num,
                    Provider = item.Provider,
                    Title = item.Title,
                    Url = item.Url
                });
            }
            return ls;
        }

        /// <summary>
        /// 无效方法
        /// </summary>
        /// <param name="ls"></param>
        /// <param name="doc"></param>
        /// <returns></returns>
        protected override List<JavVideoIndex> ParseIndex(List<JavVideoIndex> ls, HtmlDocument doc)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="url">地址</param>
        /// <returns></returns>
        public override async Task<JavVideo> Get(string url)
        {
            var m = regexFC2.Match(url);
            if (m.Success == false)
                return null;
            return await GetById(m.Groups["id"].Value);
        }

        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="url">地址</param>
        /// <returns></returns>
        private async Task<JavVideo> GetById(string id)
        {
            //https://adult.contents.fc2.com/article/1252526/
            //https://fc2club.com/html/FC2-1252526.html
            var url = $"https://fc2club.com/html/FC2-{id}.html";
            var doc = await GetHtmlDocumentAsync(url);
            if (doc == null)
                return null;

            var node = doc.DocumentNode.SelectSingleNode("//div[@class='show-top-grids']");
            if (node == null)
                return null;

            var doc2 = GetHtmlDocumentAsync($"https://adult.contents.fc2.com/article/{id}/");

            var dic = new Dictionary<string, string>();
            var nodes = node.SelectNodes(".//h5/strong/..");
            foreach (var n in nodes)
            {
                var name = n.SelectSingleNode("./strong")?.InnerText?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                //尝试获取 a 标签的内容
                var aa = n.SelectNodes("./a");
                var value = aa?.Any() == true ? string.Join(", ", aa.Select(o => o.InnerText.Trim()).Where(o => string.IsNullOrWhiteSpace(o) == false && !o.Contains("本资源")))
                    : null;

                if (string.IsNullOrWhiteSpace(value) == false)
                    dic[name] = value;
            }

            string GetValue(string _key)
                => dic.Where(o => o.Key.Contains(_key)).Select(o => o.Value).FirstOrDefault();

            var genres = GetValue("影片标签")?.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList();

            var actors = GetValue("女优名字")?.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList();

            string getDate()
            {
                var t = doc2.GetAwaiter().GetResult()?.DocumentNode.SelectSingleNode("//div[@class='items_article_Releasedate']")?.InnerText;
                if (string.IsNullOrWhiteSpace(t))
                    return null;
                var dm = regexDate.Match(t);
                if (dm.Success == false)
                    return null;
                return dm.Groups["date"].Value.Replace('/', '-');
            }

            var samples = node.SelectNodes("//ul[@class='slides']/li/img")?
                 .Select(o => o.GetAttributeValue("src", null)).Where(o => o != null).Select(o => new Uri(client.BaseAddress, o).ToString()).ToList();
            var m = new JavVideo()
            {
                Provider = Name,
                Url = url,
                Title = node.SelectSingleNode(".//h3")?.InnerText?.Trim(),
                Cover = samples?.FirstOrDefault(),
                Num = $"FC2-{id}",
                Date = getDate(),
                //Runtime = GetValue("収録時間"),
                Maker = GetValue("卖家信息"),
                Studio = GetValue("卖家信息"),
                Set = Name,
                //Director = GetValue("シリーズ"),
                //Plot = node.SelectSingleNode("//p[@class='txt introduction']")?.InnerText,
                Genres = genres,
                Actors = actors,
                Samples = samples,
            };
            //去除标题中的番号
            if (string.IsNullOrWhiteSpace(m.Num) == false && m.Title?.StartsWith(m.Num, StringComparison.OrdinalIgnoreCase) == true)
                m.Title = m.Title.Substring(m.Num.Length).Trim();

            return m;
        }
    }
}