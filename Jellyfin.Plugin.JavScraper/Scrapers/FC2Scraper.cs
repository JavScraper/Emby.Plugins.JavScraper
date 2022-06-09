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
        public FC2Scraper(ILoggerFactory loggerFactory, ApplicationDbContext applicationDbContext) : base("https://fc2club.net/", loggerFactory.CreateLogger<FC2Scraper>(), applicationDbContext)
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
                return new List<JavVideoIndex>();
            }

            var id = match.Groups["id"].Value;
            return await DoQyery(id).ConfigureAwait(false);
        }

        /// <summary>
        /// 获取列表
        /// </summary>
        /// <param name="key">关键字</param>
        /// <returns></returns>
        protected override async Task<IReadOnlyList<JavVideoIndex>> DoQyery(string key)
        {
            var vedioIndexList = new List<JavVideoIndex>();
            var item = await GetById(key).ConfigureAwait(false);
            if (item != null)
            {
                vedioIndexList.Add(new JavVideoIndex()
                {
                    Cover = item.Cover,
                    Date = item.Date,
                    Num = item.Num,
                    Provider = item.Provider,
                    Title = item.Title,
                    Url = item.Url
                });
            }

            return vedioIndexList;
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
            // https://adult.contents.fc2.com/article/1252526/
            // https://fc2club.net/html/FC2-1252526.html
            var url = $"/html/FC2-{id}.html";
            var doc = await GetHtmlDocumentAsync(url).ConfigureAwait(false);
            if (doc == null)
            {
                return null;
            }

            var node = doc.DocumentNode.SelectSingleNode("//div[@class='show-top-grids']");
            if (node == null)
            {
                return null;
            }

            var nodes = node.SelectNodes(".//h5/strong/..");
            if (nodes == null || !nodes.Any())
            {
                return null;
            }

            var dic = new Dictionary<string, string>();

            foreach (var n in nodes)
            {
                var name = n.SelectSingleNode("./strong")?.InnerText?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                // 尝试获取 a 标签的内容
                var aNodes = n.SelectNodes("./a");
                var value = aNodes?.Any() == true ? string.Join(", ", aNodes.Select(aNode => aNode.InnerText.Trim()).Where(o => !string.IsNullOrWhiteSpace(o) && !o.Contains("本资源", StringComparison.CurrentCultureIgnoreCase)))
                    : n.InnerText?.Split('：').Last();

                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                dic[name] = value;
            }

            float? GetCommunityRating()
            {
                var value = dic.GetValueOrDefault("影片评分", string.Empty);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                var m = Regex.Match(value, @"(?<rating>[\d.]+)");
                if (m.Success == false)
                {
                    return null;
                }

                if (float.TryParse(m.Groups["rating"].Value, out var rating))
                {
                    return rating / 10.0f;
                }

                return null;
            }

            async Task<string?> GetDateAsync()
            {
                var articleDoc = await GetHtmlDocumentAsync($"https://adult.contents.fc2.com/article/{id}/").ConfigureAwait(false);
                var t = articleDoc?.DocumentNode.SelectSingleNode("//div[@class='items_article_Releasedate']")?.InnerText;
                if (string.IsNullOrWhiteSpace(t))
                {
                    return null;
                }

                var match = _dateRegex.Match(t);
                if (!match.Success)
                {
                    return null;
                }

                return match.Groups["date"].Value.Replace('/', '-');
            }

            var samples = node.SelectNodes("//ul[@class='slides']/li/img")
                ?.Select(slides => slides.GetAttributeValue("src", null))
                .Where(src => !string.IsNullOrEmpty(src))
                .Select(src => new Uri(new Uri(BaseAddress), "a").ToString()).ToList()
                ?? new List<string>(0);
            var vedio = new JavVideo()
            {
                Provider = Name,
                Url = url,
                Title = node.SelectSingleNode(".//h3")?.InnerText.Trim() ?? string.Empty,
                Cover = samples.FirstOrDefault() ?? string.Empty,
                Num = $"FC2-{id}",
                Date = await GetDateAsync().ConfigureAwait(false) ?? string.Empty,
                // Runtime =dic["収録時間"),
                Maker = dic.GetValueOrDefault("卖家信息", string.Empty),
                Studio = dic.GetValueOrDefault("卖家信息", string.Empty),
                Set = Name,
                // Director =dic["シリーズ"),
                // Plot = node.SelectSingleNode("//p[@class='txt introduction']")?.InnerText,
                Genres = dic.GetValueOrDefault("影片标签", string.Empty).Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).ToList(),
                Actors = dic.GetValueOrDefault("女优名字", string.Empty).Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).ToList(),
                Samples = samples,
                CommunityRating = GetCommunityRating(),
            };
            // 去除标题中的番号
            if (!string.IsNullOrWhiteSpace(vedio.Num) && vedio.Title.StartsWith(vedio.Num, StringComparison.OrdinalIgnoreCase))
            {
                vedio.Title = vedio.Title[vedio.Num.Length..].Trim();
            }

            return vedio;
        }
    }
}
