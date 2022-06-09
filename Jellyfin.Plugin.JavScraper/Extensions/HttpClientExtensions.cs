using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Jellyfin.Plugin.JavScraper.Extensions
{
    internal static class HttpClientExtensions
    {
        public static async Task<HtmlDocument?> GetHtmlDocumentAsync(this HttpClient client, string requestUri)
        {
            var html = await client.GetStringAsync(requestUri).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(html))
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                return doc;
            }

            return null;
        }
    }
}
