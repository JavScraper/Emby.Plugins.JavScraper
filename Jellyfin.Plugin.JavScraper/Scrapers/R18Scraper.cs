using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Scrapers.Model;
using Jellyfin.Plugin.JavScraper.Services;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Scrapers
{
    /// <summary>
    /// https://www.r18.com/videos/vod/movies/detail/-/id=118abw00032/?i3_ref=search&i3_ord=1
    /// </summary>
    public class R18Scraper : AbstractScraper
    {
        public R18Scraper(ILoggerFactory loggerFactory, ApplicationDbContext applicationDbContext)
            : base("https://www.r18.com/", loggerFactory.CreateLogger<R18Scraper>(), applicationDbContext)
        {
        }

        /// <summary>
        /// 适配器名称
        /// </summary>
        public override string Name => "R18";

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
            // https://www.r18.com/common/search/searchword=ABW-032/
            var doc = await GetHtmlDocumentAsync($"/common/search/searchword={key}/?lg=zh").ConfigureAwait(false);
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
        protected override IReadOnlyCollection<JavVideoIndex> ParseIndex(HtmlDocument doc)
        {
            var indexList = new List<JavVideoIndex>();

            var nodes = doc.DocumentNode.SelectNodes("//li[@class='item-list']");
            if (nodes == null || !nodes.Any())
            {
                return indexList;
            }

            foreach (var node in nodes)
            {
                var aNode = node.SelectSingleNode("./a");
                if (aNode == null)
                {
                    continue;
                }

                var url = aNode.GetAttributeValue("href", null);
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                var imgNode = aNode.SelectSingleNode(".//img");
                if (imgNode == null)
                {
                    continue;
                }

                var index = new JavVideoIndex()
                {
                    Provider = Name,
                    Url = url + "&lg=zh",
                    Num = imgNode.GetAttributeValue("alt", string.Empty),
                    Title = aNode.SelectSingleNode(".//dt")?.InnerText.Trim() ?? string.Empty,
                    Cover = imgNode.GetAttributeValue("src", string.Empty),
                };

                index.Title = string.IsNullOrWhiteSpace(index.Title) ? index.Num : index.Title;

                indexList.Add(index);
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
            // https://www.r18.com/videos/vod/movies/detail/-/id=ssni00879/?dmmref=video.movies.popular&i3_ref=list&i3_ord=4
            var doc = await GetHtmlDocumentAsync(url).ConfigureAwait(false);
            if (doc == null)
            {
                return null;
            }

            var node = doc.DocumentNode.SelectSingleNode("//div[@class='product-details-page']");
            if (node == null)
            {
                return null;
            }

            var product_details = node.SelectSingleNode(".//div[@class='product-details']");

            string? GetValueByItemprop(string name)
                => product_details.SelectSingleNode($".//dd[@itemprop='{name}']")?.InnerText.Trim().Trim('-');

            string? GetDuration()
            {
                var duration = GetValueByItemprop("duration");
                if (string.IsNullOrWhiteSpace(duration))
                {
                    return null;
                }

                var match = Regex.Match(duration, @"[\d]+");
                if (match.Success)
                {
                    return match.Value;
                }

                return null;
            }

            var dic = new Dictionary<string, string>();
            var nodes = product_details.SelectNodes(".//dt");
            foreach (var n in nodes)
            {
                var name = n.InnerText.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                // 获取下一个标签
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
                    {
                        break;
                    }
                }
                while (true);
                if (nx == null)
                {
                    continue;
                }

                var aNodes = nx.SelectNodes(".//a");
                var value = aNodes?.Any() == true ? string.Join(", ", aNodes.Select(o => o.InnerText.Trim()?.Trim('-')).Where(o => !string.IsNullOrWhiteSpace(o)))
                    : nx?.InnerText?.Trim()?.Trim('-');

                if (!string.IsNullOrWhiteSpace(value))
                {
                    dic[name] = value;
                }
            }

            var genres = product_details.SelectNodes(".//*[@itemprop='genre']")
                ?.Select(o => o.InnerText.Trim()?.Trim('-') ?? string.Empty)
                .Where(o => !string.IsNullOrWhiteSpace(o)).ToList()
                ?? new List<string>(0);

            var actors = product_details.SelectNodes(".//div[@itemprop='actors']//*[@itemprop='name']")
                ?.Select(o => o.InnerText.Trim()?.Trim('-') ?? string.Empty)
                .Where(o => !string.IsNullOrWhiteSpace(o)).ToList()
                ?? new List<string>(0);

            var product_gallery = doc.GetElementbyId("product-gallery");
            var samples = product_gallery.SelectNodes(".//img")?.Select(o => o.GetAttributeValue("data-src", null) ?? o.GetAttributeValue("src", null) ?? string.Empty).Where(o => o != null).ToList() ?? new List<string>(0);

            var video = new JavVideo()
            {
                Provider = Name,
                Url = url,
                Title = HttpUtility.HtmlDecode(node.SelectSingleNode(".//cite")?.InnerText?.Trim() ?? string.Empty),
                Cover = node.SelectSingleNode(".//img[@itemprop='image']")?.GetAttributeValue("src", null)?.Replace("ps.", "pl.", StringComparison.OrdinalIgnoreCase) ?? string.Empty,
                Num = dic.GetValueOrDefault("DVD ID:", string.Empty),
                Date = GetValueByItemprop("dateCreated")?.Replace('/', '-') ?? string.Empty,
                Runtime = GetDuration() ?? string.Empty,
                Maker = dic.GetValueOrDefault("片商:", string.Empty),
                Studio = dic.GetValueOrDefault("廠牌:", string.Empty),
                Set = dic.GetValueOrDefault("系列:", string.Empty),
                Director = GetValueByItemprop("director") ?? string.Empty,
                // Plot = node.SelectSingleNode("//p[@class='txt introduction']")?.InnerText,
                Genres = product_details.SelectNodes(".//*[@itemprop='genre']")?.Select(o => o.InnerText.Trim()?.Trim('-') ?? string.Empty).Where(o => !string.IsNullOrWhiteSpace(o)).ToList() ?? new List<string>(0),
                Actors = actors,
                Samples = samples,
            };

            if (string.IsNullOrWhiteSpace(video.Title))
            {
                video.Title = video.Num;
            }

            if (string.IsNullOrWhiteSpace(video.Overview))
            {
                video.Overview = await GetDmmPlot(video.Num).ConfigureAwait(false) ?? string.Empty;
            }

            // 去除标题中的番号
            if (string.IsNullOrWhiteSpace(video.Num) == false && video.Title?.StartsWith(video.Num, StringComparison.OrdinalIgnoreCase) == true)
            {
                video.Title = video.Title[video.Num.Length..].Trim();
            }

            return video;
        }
    }
}
