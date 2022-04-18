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
    /// https://adult.contents.fc2.com/article/2535523/
    /// </summary>
    public class FC2Video : AbstractScraper
    {
        /// <summary>
        /// 适配器名称
        /// </summary>
        public override string Name => nameof(FC2Video);

        private static readonly Regex RegexDate = new Regex(@"(?<date>[\d]{4}[-/][\d]{2}[-/][\d]{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RegexFC2 = new Regex(@"FC2-*(PPV|)-(?<id>[\d]{2,10})($|[^\d])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RegexFC2Video = new Regex(@"article/(?<id>[\d]{2,10})($|[^\d])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        
        /// <summary>
        /// 构造
        /// </summary>
        /// <param name="handler"></param>
        public FC2Video(
#if __JELLYFIN__
            ILoggerFactory logManager
#else
            ILogManager logManager
#endif
            )
            : base("https://adult.contents.fc2.com/", logManager.CreateLogger<FC2Video>())
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
            var match = RegexFC2.Match(key);
            if (match.Success)
            {
                return DoQyery(new List<JavVideoIndex>(), match.Groups["id"].Value);
            }

            match = RegexFC2Video.Match(key);
            if (match.Success)
            {
                return DoQyery(new List<JavVideoIndex>(), match.Groups["id"].Value);
            }

            return Task.FromResult(new List<JavVideoIndex>());
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
            var match = RegexFC2.Match(url);
            if (match.Success)
            {
                return await GetById(match.Groups["id"].Value);
            }

            match = RegexFC2Video.Match(url);
            if (match.Success)
            {
                return await GetById(match.Groups["id"].Value);
            }

            return null;
        }

        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="url">地址</param>
        /// <returns></returns>
        private async Task<JavVideo> GetById(string id)
        {
            //https://adult.contents.fc2.com/article/1252526/
            var url = $"/article/{id}/";
            var doc = await GetHtmlDocumentAsync(url);
            if (doc == null)
            {
                return null;
            }

            string title = doc.DocumentNode.SelectSingleNode("//meta[@name='twitter:title']").GetAttributeValue("content", null);
            string cover = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']")?.GetAttributeValue("content", null);
            string releaseDate = doc.DocumentNode.SelectSingleNode("//div[@class='items_article_Releasedate']")?.InnerText ?? string.Empty;
            var match = RegexDate.Match(releaseDate);
            if (match.Success)
            {
                releaseDate = match.Groups["date"].Value.Replace('/', '-');
            }
            else
            {
                releaseDate = null;
            }

            string seller = doc.DocumentNode.SelectSingleNode("//section[@class='items_comment_sellerBox']//h4")?.InnerText;
            List<string> genres = doc.DocumentNode.SelectNodes("//a[@class='tag tagTag']").Select(genre => genre.InnerText).ToList();
            List<string> samples = doc.DocumentNode.SelectNodes("//section[@class='items_article_SampleImages']//a")
                .Select(sample => sample.GetAttributeValue("href", null))
                .Where(sample => string.IsNullOrWhiteSpace(sample))
                .ToList();

            var javVideo = new JavVideo()
            {
                Provider = Name,
                Url = url,
                Title = title,
                Cover = cover,
                Num = $"FC2-{id}",
                Date = releaseDate,
                Maker = seller,
                Studio = seller,
                Set = "FC2",
                Genres = genres,
                Samples = samples,
            };

            return javVideo;
        }
    }
}