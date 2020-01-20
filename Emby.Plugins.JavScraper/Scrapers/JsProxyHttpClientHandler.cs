using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper.Scrapers
{
    /// <summary>
    /// Js Proxy 客户端
    /// </summary>
    public class JsProxyHttpClientHandler : HttpClientHandler
    {
        /// <summary>
        /// 获取重定向url
        /// </summary>
        private static Regex regexUrl = new Regex(@"href *=["" ]*(?<url>[^"" >]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// 发送请求
        /// </summary>
        /// <param name="request">请求信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.TryAddWithoutValidation("X-FORWARDED-FOR", "8.8.8.8");

            if (Plugin.Instance.Configuration.HasJsProxy == false)
                return await base.SendAsync(request, cancellationToken);

            var jsproxy_url = Plugin.Instance.Configuration.JsProxy;
            // Add header to request here
            var url = request.RequestUri.ToString();
            if (url.StartsWith(jsproxy_url, StringComparison.OrdinalIgnoreCase) != true)
            {
                url = Plugin.Instance.Configuration.BuildProxyUrl(url);
                request.RequestUri = new Uri(url);
                url = request.Headers.Referrer?.ToString();
                if (!(url?.IndexOf("?") > 0))
                    request.Headers.Referrer = new Uri("https://www.google.com/?--ver=110&--mode=navigate&--type=document&origin=&--aceh=1&dnt=1&upgrade-insecure-requests=1&cookie=has_recent_activity%3D1%3B+has_recent_activity%3D1&--level=0"); ;
            }
            var resp = await base.SendAsync(request, cancellationToken);

            //不需要重定向
            if (string.Compare(resp.ReasonPhrase, "Redirection", true) != 0)
                return resp;

            var html = await resp.Content.ReadAsStringAsync();

            var m = regexUrl.Match(html);
            if (m.Success)
            {
                request.RequestUri = new Uri(m.Groups["url"].Value);
                return await SendAsync(request, cancellationToken);
            }

            return resp;
        }
    }
}