using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jellyfin.Plugin.JavScraper.Extensions;
using Jellyfin.Plugin.JavScraper.Http;
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
        private readonly IHttpClientManager _httpClientManager;

        public FC2Scraper(ILogger logger, IHttpClientManager httpClientManager, DMMService dmmService)
            : base("https://adult.contents.fc2.com", logger, dmmService)
        {
            _httpClientManager = httpClientManager;
        }

        public override string Name => "FC2";

        protected override bool IsKeyValid(string key)
            => JavIdRecognizer.FC2(key) != null;

        protected override async Task<IReadOnlyCollection<JavVideoIndex>> DoSearch(string key)
        {
            var vedio = await GetById(key).ConfigureAwait(false);
            if (vedio == null)
            {
                return Array.Empty<JavVideoIndex>();
            }

            return new List<JavVideoIndex>
            {
                vedio
            };
        }

        protected override async Task<JavVideo?> DoGetJavVideo(JavVideoIndex index) => await GetById(index.Num).ConfigureAwait(false);

        private async Task<JavVideo?> GetById(string id)
        {
            var match = _regexFC2.Match(id);
            if (!match.Success)
            {
                return null;
            }

            id = match.Groups["id"].Value;

            // https://adult.contents.fc2.com/article/2543981/
            // https://fc2club.net/html/FC2-1252526.html
            var url = new Uri(BaseAddress, $"/article/{id}/");
            var doc = await _httpClientManager.GetClient().GetHtmlDocumentAsync(url).ConfigureAwait(false);
            if (doc == null)
            {
                return null;
            }

            return new JavVideo
            {
                Provider = Name,
                Url = url.ToString(),
                Num = $"FC2-{id}",
                Title = doc.DocumentNode.SelectSingleNode("//meta[@name='twitter:title']").GetAttributeValue("content", null)?.Trim() ?? string.Empty,
                Cover = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']")?.GetAttributeValue("content", null).Trim() ?? string.Empty,
                Date = doc.DocumentNode.SelectSingleNode("//div[@class='items_article_Releasedate']")?.InnerText.TryMatch(_dateRegex, out match) == true ? match.Groups["date"].Value.Replace('/', '-') : string.Empty,
                Maker = doc.DocumentNode.SelectSingleNode("//section[@class='items_comment_sellerBox']//h4")?.InnerText?.Trim() ?? string.Empty,
                Studio = doc.DocumentNode.SelectSingleNode("//section[@class='items_comment_sellerBox']//h4")?.InnerText?.Trim() ?? string.Empty,
                Set = "FC2",
                // Genres = genres,
                Samples = doc.DocumentNode.SelectNodes("//section[@class='items_article_SampleImages']//a")
                        ?.Select(sample => sample.GetAttributeValue("href", null))
                        .Where(string.IsNullOrWhiteSpace)
                        .ToArray()
                    ?? Array.Empty<string>(),
            };
        }
    }
}
