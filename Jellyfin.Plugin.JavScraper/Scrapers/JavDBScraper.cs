using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Http;
using Jellyfin.Plugin.JavScraper.Scrapers.Model;
using Jellyfin.Plugin.JavScraper.Services;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Scrapers
{
    /// <summary>
    /// https://www.javbus.com/BIJN-172
    /// </summary>
    public class JavDBScraper : AbstractScraper
    {
        /// <summary>
        /// 番号分段识别
        /// </summary>
        private static readonly Regex _regex = new("((?<a>[a-z]{2,})|(?<b>[0-9]{2,}))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// 构造
        /// </summary>
        public JavDBScraper(ILoggerFactory loggerFactory, ApplicationDbContext applicationDbContext, ICustomHttpClientFactory clientFactory)
            : base("https://javdb8.com/", loggerFactory.CreateLogger<JavDBScraper>(), applicationDbContext, clientFactory)
        {
        }

        /// <summary>
        /// 适配器名称
        /// </summary>
        public override string Name => "JavDB";

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
            // https://javdb.com/search?q=ADN-106&f=all
            var doc = await GetHtmlDocumentAsync($"/search?q={key}&f=all").ConfigureAwait(false);
            if (doc == null)
            {
                return new List<JavVideoIndex>(0);
            }

            var keySegments = _regex.Matches(key).Cast<Match>().Select(o => o.Groups[0].Value.TrimStart('0')).ToList();
            return SortIndex(key, ParseIndex(doc).Where(index => keySegments.All(keySegment => index.Num.Contains(keySegment, StringComparison.OrdinalIgnoreCase))));
        }

        /// <summary>
        /// 解析列表
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        protected override List<JavVideoIndex> ParseIndex(HtmlDocument doc)
        {
            var indexList = new List<JavVideoIndex>();

            var nodes = doc.DocumentNode.SelectNodes("//*[@id='videos']/div/div/a");
            if (nodes == null || !nodes.Any())
            {
                return indexList;
            }

            foreach (var node in nodes)
            {
                var url = node.GetAttributeValue("href", string.Empty);
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                var index = new JavVideoIndex() { Provider = Name, Url = new Uri(BaseAddress, url).ToString() };
                indexList.Add(index);
                var imgNode = node.SelectSingleNode("./div/img");
                if (imgNode != null)
                {
                    var imgUrl = imgNode.GetAttributeValue("data-original", string.Empty);
                    if (string.IsNullOrEmpty(imgUrl))
                    {
                        imgUrl = imgNode.GetAttributeValue("data-src", string.Empty);
                    }

                    if (string.IsNullOrEmpty(imgUrl))
                    {
                        imgUrl = imgNode.GetAttributeValue("src", string.Empty);
                    }

                    if (imgUrl.StartsWith("//", StringComparison.Ordinal))
                    {
                        index.Cover = $"https:{index.Cover}";
                    }
                }

                index.Num = node.SelectSingleNode("./div[@class='uid']")?.InnerText.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(index.Num))
                {
                    index.Num = node.SelectSingleNode("./div[@class='uid2']")?.InnerText.Trim() ?? string.Empty;
                }

                index.Title = node.SelectSingleNode("./div[@class='video-title']")?.InnerText.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(index.Title))
                {
                    index.Title = node.SelectSingleNode("./div[@class='video-title2']")?.InnerText.Trim() ?? string.Empty;
                }

                index.Date = node.SelectSingleNode("./div[@class='meta']")?.InnerText.Trim() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(index.Num) && index.Title.StartsWith(index.Num, StringComparison.OrdinalIgnoreCase))
                {
                    index.Title = index.Title.Substring(index.Num.Length).Trim();
                }
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
            // https://javdb.com/v/BzbA6
            var doc = await GetHtmlDocumentAsync(url).ConfigureAwait(false);
            if (doc == null)
            {
                return null;
            }

            var nodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'panel-block')]");
            if (nodes == null || !nodes.Any())
            {
                return null;
            }

            var dic = new Dictionary<string, string>();
            foreach (var node in nodes)
            {
                var key = node.SelectSingleNode("./strong")?.InnerText?.Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (key.Contains("演員", StringComparison.CurrentCultureIgnoreCase))
                {
                    var actor = node.SelectNodes("./*[@class='value']/a");
                    if (actor?.Any() == true)
                    {
                        dic[key] = string.Join(",", actor.Select(o => o.InnerText?.Trim()));
                        continue;
                    }
                }

                var value = node.SelectSingleNode("./*[@class='value']")?.InnerText?.Trim().Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    dic[key] = value;
                }
            }

            string? GetCover()
            {
                var coverNode = doc.DocumentNode.SelectSingleNode("//img[contains(@class,'video-cover')]");
                if (coverNode == null)
                {
                    return null;
                }

                var img = coverNode.GetAttributeValue("data-original", null);
                if (string.IsNullOrEmpty(img))
                {
                    img = coverNode.GetAttributeValue("data-src", null);
                }

                if (string.IsNullOrEmpty(img))
                {
                    img = coverNode.GetAttributeValue("src", null);
                }

                if (!string.IsNullOrWhiteSpace(img))
                {
                    return img;
                }

                img = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']")?.GetAttributeValue("content", null);
                if (!string.IsNullOrWhiteSpace(img))
                {
                    return img;
                }

                return doc.DocumentNode.SelectSingleNode("//meta[@class='column column-video-cover']")?.GetAttributeValue("poster", null);
            }

            float? GetCommunityRating()
            {
                var value = dic.GetValueOrDefault("評分", string.Empty);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                var match = Regex.Match(value, @"(?<rating>[\d.]+)分");
                if (!match.Success)
                {
                    return null;
                }

                if (float.TryParse(match.Groups["rating"].Value, out var rating))
                {
                    return rating / 5.0f * 10f;
                }

                return null;
            }

            List<string> GetSamples()
                => doc.DocumentNode.SelectNodes("//div[@class='tile-images preview-images']/a")
                      ?.Select(o => o.GetAttributeValue("href", null))
                      .Where(o => string.IsNullOrWhiteSpace(o) == false)
                      .Where(o => !o.StartsWith('#')).ToList()
                      ?? new List<string>(0);

            var m = new JavVideo()
            {
                Provider = Name,
                Url = url,
                Title = doc.DocumentNode.SelectSingleNode("//*[contains(@class,'title')]/strong")?.InnerText?.Trim() ?? string.Empty,
                Cover = GetCover() ?? string.Empty,
                Num = dic.GetValueOrDefault("番號", string.Empty),
                Date = dic.GetValueOrDefault("日期", string.Empty),
                Runtime = dic.GetValueOrDefault("時長", string.Empty),
                Maker = dic.GetValueOrDefault("發行", string.Empty),
                Studio = dic.GetValueOrDefault("片商", string.Empty),
                Set = dic.GetValueOrDefault("系列", string.Empty),
                Director = dic.GetValueOrDefault("導演", string.Empty),
                Genres = dic.GetValueOrDefault("類別", string.Empty).Split(',').Select(o => o.Trim()).Distinct().ToList(),
                Actors = dic.GetValueOrDefault("演員", string.Empty).Split(',').Select(o => o.Trim().Trim('(').Trim(')')).Distinct().ToList(),
                Samples = GetSamples(),
                CommunityRating = GetCommunityRating(),
            };

            m.Overview = await GetDmmPlot(m.Num).ConfigureAwait(false) ?? string.Empty;
            ////去除标题中的番号
            if (string.IsNullOrWhiteSpace(m.Num) == false && m.Title?.StartsWith(m.Num, StringComparison.OrdinalIgnoreCase) == true)
            {
                m.Title = m.Title.Substring(m.Num.Length).Trim();
            }

            return m;
        }
    }
}
