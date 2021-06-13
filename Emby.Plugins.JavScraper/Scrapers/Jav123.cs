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
    /// https://www.jav321.com/
    /// </summary>
    public class Jav123 : AbstractScraper
    {
        /// <summary>
        /// 适配器名称
        /// </summary>
        public override string Name => "Jav123";

        /// <summary>
        /// 构造
        /// </summary>
        /// <param name="handler"></param>
        public Jav123(
#if __JELLYFIN__
            ILoggerFactory logManager
#else
            ILogManager logManager
#endif
            )
            : base("https://www.jav321.com/", logManager.CreateLogger<Jav123>())
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
            ///https://www.jav321.com/search
            ///POST sn=key
            var doc = await GetHtmlDocumentByPostAsync($"/search", new Dictionary<string, string>() { ["sn"] = key });
            if (doc != null)
            {
                var video = await ParseVideo(null, doc);
                if (video != null)
                    ls.Add(video);
            }

            SortIndex(key, ls);
            return ls;
        }

        /// <summary>
        /// 不用了
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
            //https://javdb.com/v/BzbA6
            var doc = await GetHtmlDocumentAsync(url);
            if (doc == null)
                return null;

            return await ParseVideo(url, doc);
        }

        private async Task<JavVideo> ParseVideo(string url, HtmlDocument doc)
        {
            var node = doc.DocumentNode.SelectSingleNode("//div[@class='panel-heading']/h3/../..");
            if (node == null)
                return null;
            var nodes = node.SelectNodes(".//b");
            if (nodes?.Any() != true)
                return null;

            if (string.IsNullOrWhiteSpace(url))
            {
                url = doc.DocumentNode.SelectSingleNode("//li/a[contains(text(),'简体中文')]")?.GetAttributeValue("href", null);
                if (url?.StartsWith("//") == true)
                    url = $"https:{url}";
            }

            var dic = new Dictionary<string, string>();
            foreach (var n in nodes)
            {
                var name = n.InnerText.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                var arr = new List<string>();

                var next = n.NextSibling;
                while (next != null && next.Name != "b")
                {
                    arr.Add(next.InnerText);
                    next = next.NextSibling;
                }
                if (arr.Count == 0)
                    continue;

                var value = string.Join(", ", arr.Select(o => o.Replace("&nbsp;", " ").Trim(": ".ToArray())).Where(o => string.IsNullOrWhiteSpace(o) == false));

                if (string.IsNullOrWhiteSpace(value))
                    continue;

                dic[name] = value;
            }

            string GetValue(string _key)
                => dic.Where(o => o.Key.Contains(_key)).Select(o => o.Value).FirstOrDefault();

            string GetCover()
            {
                var img = node.SelectSingleNode(".//*[@id='vjs_sample_player']")?.GetAttributeValue("poster", null);
                if (string.IsNullOrWhiteSpace(img) == false)
                    return img;
                if (string.IsNullOrWhiteSpace(img) == false)
                    return img;
                img = node.SelectSingleNode(".//*[@id='video-player']")?.GetAttributeValue("poster", null);
                img = doc.DocumentNode.SelectSingleNode("//img[@class='img-responsive']")?.GetAttributeValue("src", null);
                if (string.IsNullOrWhiteSpace(img) == false)
                    return img;
                return img;
            }

            List<string> GetGenres()
            {
                var v = GetValue("ジャンル");
                if (string.IsNullOrWhiteSpace(v))
                    return null;
                return v.Split(',').Select(o => o.Trim()).Distinct().ToList();
            }

            List<string> GetActors()
            {
                var v = GetValue("出演者");
                if (string.IsNullOrWhiteSpace(v))
                    return null;
                var ac = v.Split(',').Select(o => o.Trim()).Distinct().ToList();
                return ac;
            }
            List<string> GetSamples()
            {
                return doc.DocumentNode.SelectNodes("//a[contains(@href,'snapshot')]/img")
                      ?.Select(o => o.GetAttributeValue("src", null))
                      .Where(o => string.IsNullOrWhiteSpace(o) == false).ToList();
            }

            var m = new JavVideo()
            {
                Provider = Name,
                Url = url,
                Title = node.SelectSingleNode(".//h3/text()")?.InnerText?.Trim(),
                Cover = GetCover(),
                Num = GetValue("品番")?.ToUpper(),
                Date = GetValue("配信開始日"),
                Runtime = GetValue("収録時間"),
                Maker = GetValue("メーカー"),
                Studio = GetValue("メーカー"),
                Set = GetValue("シリーズ"),
                Director = GetValue("导演"),
                Genres = GetGenres(),
                Actors = GetActors(),
                Samples = GetSamples(),
                Plot = node.SelectSingleNode("./div[@class='panel-body']/div[last()]")?.InnerText?.Trim(),
            };
            if (string.IsNullOrWhiteSpace(m.Plot))
                m.Plot = await GetDmmPlot(m.Num);
            ////去除标题中的番号
            if (string.IsNullOrWhiteSpace(m.Num) == false && m.Title?.StartsWith(m.Num, StringComparison.OrdinalIgnoreCase) == true)
                m.Title = m.Title.Substring(m.Num.Length).Trim();

            return m;
        }
    }
}