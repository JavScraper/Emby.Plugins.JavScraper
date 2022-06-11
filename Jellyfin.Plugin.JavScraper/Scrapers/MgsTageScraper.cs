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
    /// https://www.mgstage.com/product/product_detail/320MMGH-242/
    /// </summary>
    public class MgsTageScraper : AbstractScraper
    {
        private static readonly Regex _regexDate = new(@"(?<date>[\d]{4}[-/][\d]{2}[-/][\d]{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// 构造
        /// </summary>
        public MgsTageScraper(ILoggerFactory loggerFactory, ApplicationDbContext applicationDbContext, IHttpClientFactory clientFactory)
            : base("https://www.mgstage.com/", loggerFactory.CreateLogger<MgsTageScraper>(), applicationDbContext, clientFactory)
        {
        }

        /// <summary>
        /// 适配器名称
        /// </summary>
        public override string Name => "MgsTage";

        /// <summary>
        /// 检查关键字是否符合
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        protected override bool CheckKey(string key)
            => JavIdRecognizer.FC2(key) == null;

        /// <summary>
        /// 获取列表
        /// </summary>
        /// <param name="key">关键字</param>
        /// <returns></returns>
        protected override async Task<IReadOnlyList<JavVideoIndex>> DoQyery(string key)
        {
            // https://www.mgstage.com/search/search.php?search_word=320MMGH-242&disp_type=detail
            var doc = await GetHtmlDocumentAsync($"/search/search.php?search_word={key}&disp_type=detail").ConfigureAwait(false);
            if (doc == null)
            {
                return new List<JavVideoIndex>(0);
            }

            return SortIndex(key, ParseIndex(doc));
        }

        /// <summary>
        /// 解析列表
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        protected override List<JavVideoIndex> ParseIndex(HtmlDocument doc)
        {
            var indexList = new List<JavVideoIndex>();

            var nodes = doc.DocumentNode.SelectNodes("//div[@class='rank_list']/ul/li");
            if (nodes == null || !nodes.Any())
            {
                return indexList;
            }

            foreach (var node in nodes)
            {
                var title_node = node.SelectSingleNode("./h5/a");
                if (title_node == null)
                {
                    continue;
                }

                var url = title_node.GetAttributeValue("href", null);
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                var videoIndex = new JavVideoIndex()
                {
                    Provider = Name,
                    Url = new Uri(BaseAddress, url).ToString(),
                    Num = url.Split("/", StringSplitOptions.RemoveEmptyEntries).Last(),
                    Title = title_node.InnerText.Trim(),
                    Cover = node.SelectSingleNode("./h6/a/img")?.GetAttributeValue("src", null) ?? string.Empty
                };

                var dateNode = node.SelectSingleNode(".//p[@class='data']");
                if (dateNode != null)
                {
                    var dateString = dateNode.InnerText.Trim();
                    var match = _regexDate.Match(dateString);
                    if (match.Success)
                    {
                        videoIndex.Date = match.Groups["date"].Value.Replace('/', '-');
                    }
                }

                indexList.Add(videoIndex);
            }

            return indexList;
        }

        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="url">地址</param>
        /// <returns></returns>
        protected override async Task<JavVideo?> GetJavVedio(string url)
        {
            // https://www.mgstage.com/product/product_detail/320MMGH-242/
            var doc = await GetHtmlDocumentAsync(url).ConfigureAwait(false);
            if (doc == null)
            {
                return null;
            }

            var node = doc.DocumentNode.SelectSingleNode("//div[@class='common_detail_cover']");
            if (node == null)
            {
                return null;
            }

            var nodes = node.SelectNodes(".//table/tr/th/..");
            if (nodes == null || !nodes.Any())
            {
                return null;
            }

            var dic = new Dictionary<string, string>();
            foreach (var row in nodes)
            {
                var name = row.SelectSingleNode("./th")?.InnerText?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                // 尝试获取 a 标签的内容
                var aNodes = row.SelectNodes("./td/a");
                var value = aNodes?.Any() == true ? string.Join(", ", aNodes.Select(o => o.InnerText.Trim()).Where(o => !string.IsNullOrWhiteSpace(o))) : row.SelectSingleNode("./td")?.InnerText?.Trim();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    dic[name] = value;
                }
            }

            var vedio = new JavVideo()
            {
                Provider = Name,
                Url = url,
                Title = node.SelectSingleNode("./h1")?.InnerText?.Trim() ?? string.Empty,
                Cover = node.SelectSingleNode(".//img[@class='enlarge_image']")?.GetAttributeValue("src", null) ?? string.Empty,
                Num = dic.GetValueOrDefault("品番", string.Empty),
                Date = dic.GetValueOrDefault("配信開始日", string.Empty).Replace('/', '-'),
                Runtime = dic.GetValueOrDefault("収録時間", string.Empty),
                Maker = dic.GetValueOrDefault("發行商", string.Empty),
                Studio = dic.GetValueOrDefault("メーカー", string.Empty),
                Set = dic.GetValueOrDefault("シリーズ", string.Empty),
                Director = dic.GetValueOrDefault("シリーズ", string.Empty),
                Overview = node.SelectSingleNode("//p[@class='txt introduction']")?.InnerText ?? string.Empty,
                Genres = dic.GetValueOrDefault("ジャンル", string.Empty).Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList(),
                Actors = dic.GetValueOrDefault("出演", string.Empty).Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList(),
                Samples = node.SelectNodes("//a[@class='sample_image']")?.Select(node => node.GetAttributeValue("href", null)).Where(href => string.IsNullOrWhiteSpace(href)).ToList() ?? new List<string>(0),
            };

            if (string.IsNullOrWhiteSpace(vedio.Overview))
            {
                vedio.Overview = await GetDmmPlot(vedio.Num).ConfigureAwait(false) ?? string.Empty;
            }

            // 去除标题中的番号
            if (string.IsNullOrWhiteSpace(vedio.Num) == false && vedio.Title?.StartsWith(vedio.Num, StringComparison.OrdinalIgnoreCase) == true)
            {
                vedio.Title = vedio.Title.Substring(vedio.Num.Length).Trim();
            }

            return vedio;
        }
    }
}
