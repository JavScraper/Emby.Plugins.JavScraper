using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Scrapers.Model;
using Jellyfin.Plugin.JavScraper.Services;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Scrapers
{
    /// <summary>
    /// https://fc2club.net/html/FC2-1249328.html
    /// </summary>
    public class FC2Scraper : AbstractScraper
    {
        private static readonly Regex _dateRegex = new(@"(?<date>[\d]{4}[-/][\d]{2}[-/][\d]{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _regexFC2 = new(@"FC2-*(PPV|)-(?<id>[\d]{2,10})($|[^\d])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// 构造
        /// </summary>
        public FC2Scraper(ILoggerFactory loggerFactory, ApplicationDbContext applicationDbContext, IHttpClientFactory clientFactory)
            : base("https://adult.contents.fc2.com", loggerFactory.CreateLogger<FC2Scraper>(), applicationDbContext, clientFactory)
        {
        }

        /// <summary>
        /// 适配器名称
        /// </summary>
        public override string Name => "FC2";

        /// <summary>
        /// 检查关键字是否符合
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        protected override bool CheckKey(string key)
            => JavIdRecognizer.FC2(key) != null;

        public override async Task<IReadOnlyList<JavVideoIndex>> Query(string key)
        {
            var match = _regexFC2.Match(key);
            if (!match.Success)
            {
                return Array.Empty<JavVideoIndex>();
            }

            return await DoQyery(match.Groups["id"].Value).ConfigureAwait(false);
        }

        /// <summary>
        /// 获取列表
        /// </summary>
        /// <param name="key">关键字</param>
        /// <returns></returns>
        protected override async Task<IReadOnlyList<JavVideoIndex>> DoQyery(string key)
        {
            var item = await GetById(key).ConfigureAwait(false);
            if (item == null)
            {
                return Array.Empty<JavVideoIndex>();
            }

            return new List<JavVideoIndex>()
                {
                    new JavVideoIndex()
                    {
                        Cover = item.Cover,
                        Date = item.Date,
                        Num = item.Num,
                        Provider = item.Provider,
                        Title = item.Title,
                        Url = item.Url
                    }
                };
        }

        /// <summary>
        /// 无效方法
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
            var match = _regexFC2.Match(url);
            if (!match.Success)
            {
                return null;
            }

            return await GetById(match.Groups["id"].Value).ConfigureAwait(false);
        }

        /// <summary>
        /// 获取详情
        /// </summary>
        /// <returns></returns>
        private async Task<JavVideo?> GetById(string id)
        {
            // https://adult.contents.fc2.com/article/2543981/
            // https://fc2club.net/html/FC2-1252526.html
            var url = $"/article/{id}/";
            var doc = await GetHtmlDocumentAsync(url).ConfigureAwait(false);
            if (doc == null)
            {
                return null;
            }

            string title = doc.DocumentNode.SelectSingleNode("//meta[@name='twitter:title']").GetAttributeValue("content", null) ?? string.Empty;
            string cover = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']")?.GetAttributeValue("content", null) ?? string.Empty;
            string releaseDate = doc.DocumentNode.SelectSingleNode("//div[@class='items_article_Releasedate']")?.InnerText ?? string.Empty;
            var match = _dateRegex.Match(releaseDate);
            if (match.Success)
            {
                releaseDate = match.Groups["date"].Value.Replace('/', '-');
            }
            else
            {
                releaseDate = string.Empty;
            }

            string seller = doc.DocumentNode.SelectSingleNode("//section[@class='items_comment_sellerBox']//h4")?.InnerText ?? string.Empty;
            // List<string> genres = doc.DocumentNode.SelectNodes("//a[@class='tag tagTag']").Select(genre => genre.InnerText).ToList();
            List<string> samples = doc.DocumentNode.SelectNodes("//section[@class='items_article_SampleImages']//a")
                .Select(sample => sample.GetAttributeValue("href", null))
                .Where(sample => string.IsNullOrWhiteSpace(sample))
                .ToList();

            var javVideo = new JavVideo()
            {
                Provider = Name,
                Url = new Uri(BaseAddress, url).ToString(),
                Title = title,
                Cover = cover,
                Num = $"FC2-{id}",
                Date = releaseDate,
                Maker = seller,
                Studio = seller,
                Set = "FC2",
                // Genres = genres,
                Samples = samples,
            };

            return javVideo;
        }
    }
}
