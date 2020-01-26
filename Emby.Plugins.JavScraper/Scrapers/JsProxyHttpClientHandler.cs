using System;
using System.Linq;
using System.Net.Http;
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
        /// 发送请求
        /// </summary>
        /// <param name="request">请求信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.TryAddWithoutValidation("X-FORWARDED-FOR", "17.172.224.88");

            if (Plugin.Instance.Configuration.HasJsProxy == false)
                return base.SendAsync(request, cancellationToken);

            var jsproxy_url = Plugin.Instance.Configuration.JsProxy;
            // Add header to request here
            var url = request.RequestUri.ToString();
            var org_url = url;
            var i = org_url.IndexOf("/http/", StringComparison.CurrentCultureIgnoreCase);
            if (i > 0)
                org_url = org_url.Substring(i + 6);

            //netcdn 这个域名不走代理
            if (request.RequestUri.Host.IndexOf("netcdn.", StringComparison.CurrentCultureIgnoreCase) > 0)
            {
                if (url != org_url)
                    request.RequestUri = new Uri(org_url);
            }
            else if (url.StartsWith(jsproxy_url, StringComparison.OrdinalIgnoreCase) != true)
            {
                url = Plugin.Instance.Configuration.BuildProxyUrl(url);
                request.RequestUri = new Uri(url);
            }

            url = request.Headers.Referrer?.ToString();
            if (!(url?.IndexOf("?") > 0))
                request.Headers.Referrer = new Uri(org_url);

            //mgstage.com 加入年龄认证Cookies
            if (request.RequestUri.ToString().Contains("mgstage.com") && !(request.Headers.TryGetValues("Cookie", out var cookies) && cookies.Contains("abc=1")))
                request.Headers.Add("Cookie", "adc=1");

            // Add UserAgent
            if (!(request.Headers.UserAgent?.Count() > 0))
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.130 Safari/537.36");

            return base.SendAsync(request, cancellationToken);
        }
    }
}