using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Jellyfin.Plugin.JavScraper.Extensions;
using Jellyfin.Plugin.JavScraper.Http;
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
        private readonly IHttpClientManager _httpClientManager;

        public R18Scraper(ILogger logger, IHttpClientManager httpClientManager, DMMService dmmService)
            : base("https://www.r18.com/", logger, dmmService)
        {
            _httpClientManager = httpClientManager;
        }

        public override string Name => "R18";

        protected override bool IsKeyValid(string key)
            => JavIdRecognizer.FC2(key) == null;

        protected override async Task<IReadOnlyCollection<JavVideoIndex>> DoSearch(string key)
        {
            // https://www.r18.com/common/search/searchword=ABW-032/
            var doc = await _httpClientManager.GetClient().GetHtmlDocumentAsync(new Uri(BaseAddress, $"/common/search/searchword={key}/?lg=zh")).ConfigureAwait(false);
            if (doc == null)
            {
                return Array.Empty<JavVideoIndex>();
            }

            return ParseIndex(doc);
        }

        private IReadOnlyCollection<JavVideoIndex> ParseIndex(HtmlDocument doc)
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
                    Url = url + "?lg=zh",
                    Num = imgNode.GetAttributeValue("alt", string.Empty),
                    Title = aNode.SelectSingleNode(".//dt")?.InnerText.Trim() ?? string.Empty,
                    Cover = imgNode.GetAttributeValue("src", string.Empty),
                };

                index.Title = string.IsNullOrWhiteSpace(index.Title) ? index.Num : index.Title;

                indexList.Add(index);
            }

            return indexList;
        }

        protected override async Task<JavVideo?> DoGetJavVideo(JavVideoIndex index)
        {
            // https://www.r18.com/videos/vod/movies/detail/-/id=ssni00879/?dmmref=video.movies.popular&i3_ref=list&i3_ord=4
            var contentId = index.Url.Split("/").FirstOrDefault(segment => segment.StartsWith("id=", StringComparison.OrdinalIgnoreCase))?[3..];
            if (string.IsNullOrEmpty(contentId))
            {
                return null;
            }

            var json = await _httpClientManager.GetClient().GetStringAsync(new Uri(BaseAddress, $"/api/v4f/contents/{contentId}?lang=zh&unit=USD")).ConfigureAwait(false);

            JsonDocument jsonDocument = JsonDocument.Parse(json);
            if (jsonDocument.RootElement.TryGetProperty("data", out var data))
            {
                return new JavVideo
                {
                    Provider = Name,
                    Url = index.Url,
                    Title = data.GetPropertyOrNull("title")?.GetString() ?? string.Empty,
                    Cover = data.GetPropertyOrNull("images")?.GetPropertyOrNull("jacket_image")?.GetPropertyOrNull("large")?.GetString() ?? string.Empty,
                    Num = data.GetPropertyOrNull("dvd_id")?.GetString() ?? string.Empty,
                    Date = data.GetPropertyOrNull("release_date")?.GetString() ?? string.Empty,
                    Runtime = (data.GetPropertyOrNull("runtime_minutes")?.GetString() ?? string.Empty) + "分钟",
                    Maker = data.GetPropertyOrNull("maker")?.GetPropertyOrNull("name")?.GetString() ?? string.Empty,
                    Studio = data.GetPropertyOrNull("label")?.GetPropertyOrNull("name")?.GetString() ?? string.Empty,
                    Director = data.GetPropertyOrNull("director")?.GetString() ?? string.Empty,
                    Set = data.GetPropertyOrNull("series")?.GetPropertyOrNull("name")?.GetString() ?? string.Empty,
                    Genres = data.GetPropertyOrNull("categories")?.EnumerateArray().Select(category => category.GetPropertyOrNull("name")?.GetString() ?? string.Empty).Where(name => !string.IsNullOrEmpty(name)).ToArray() ?? Array.Empty<string>(),
                    Actors = data.GetPropertyOrNull("actresses")?.EnumerateArray().Select(category => category.GetPropertyOrNull("name")?.GetString() ?? string.Empty).Where(name => !string.IsNullOrEmpty(name)).ToArray() ?? Array.Empty<string>(),
                    Samples = data.GetPropertyOrNull("gallery")?.EnumerateArray().Select(large => large.GetPropertyOrNull("large")?.GetString() ?? string.Empty).Where(name => !string.IsNullOrEmpty(name)).ToArray() ?? Array.Empty<string>(),
                };
            }

            return null;
        }
    }
}
