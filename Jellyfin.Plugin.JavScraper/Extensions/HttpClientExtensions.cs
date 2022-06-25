using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Jellyfin.Plugin.JavScraper.Extensions
{
    internal static class HttpClientExtensions
    {
        public static async Task<HtmlDocument?> SendAndReturnHtmlDocumentAsync(this HttpClient client, HttpRequestMessage request, bool parseWhenNoSuccessStatuCode = false)
        {
            var response = await client.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode && !parseWhenNoSuccessStatuCode)
            {
                return null;
            }

            var html = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return doc;
        }

        public static async Task<HtmlDocument?> GetHtmlDocumentAsync(this HttpClient client, string requestUri, bool parseWhenNoSuccessStatuCode = false)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            return await client.SendAndReturnHtmlDocumentAsync(request, parseWhenNoSuccessStatuCode).ConfigureAwait(false);
        }

        public static Task<HtmlDocument?> GetHtmlDocumentAsync(this HttpClient client, Uri requestUri, bool parseWhenNoSuccessStatuCode = false) =>
            client.GetHtmlDocumentAsync(requestUri.ToString(), parseWhenNoSuccessStatuCode);

        public static async Task<HtmlDocument?> GetHtmlDocumentByPostFormAsync(this HttpClient client, Uri requestUri, Dictionary<string, string> formParam)
        {
            using var content = new FormUrlEncodedContent(formParam);
            return await GetHtmlDocumentByPostFormAsync(client, requestUri, content).ConfigureAwait(false);
        }

        public static async Task<HtmlDocument?> GetHtmlDocumentByPostFormAsync(this HttpClient client, Uri requestUri, HttpContent content)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Content = content;
            return await client.SendAndReturnHtmlDocumentAsync(request).ConfigureAwait(false);
        }
    }
}
