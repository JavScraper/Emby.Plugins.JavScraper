using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper.Http
{
    /// <summary>
    /// Proxy 客户端
    /// </summary>
    public class ProxyHttpClientHandler : HttpClientHandler
    {
        public ProxyHttpClientHandler()
        {
            //忽略SSL证书问题
            ServerCertificateCustomValidationCallback = (message, certificate2, arg3, arg4) => true;
            Proxy = new JavWebProxy();
            UseProxy = true;
        }

        /// <summary>
        /// 发送请求
        /// </summary>
        /// <param name="request">请求信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var cfg = Plugin.Instance.Configuration;

            request.Headers.Remove("X-FORWARDED-FOR");
            if (cfg.EnableX_FORWARDED_FOR && !string.IsNullOrWhiteSpace(cfg.X_FORWARDED_FOR) &&
                IPAddress.TryParse(cfg.X_FORWARDED_FOR, out var _))
                request.Headers.TryAddWithoutValidation("X-FORWARDED-FOR", cfg.X_FORWARDED_FOR);

            //mgstage.com 加入年龄认证Cookies
            if (request.RequestUri.ToString().Contains("mgstage.com") && !(request.Headers.TryGetValues("Cookie", out var cookies) && cookies.Contains("abc=1")))
                request.Headers.Add("Cookie", "adc=1");

            //dmm.co.jp 加入年龄认证Cookies
            if (request.RequestUri.ToString().Contains("dmm.co.jp") && !(request.Headers.TryGetValues("Cookie", out var cookies2) && cookies2.Contains("age_check_done=1")))
                request.Headers.Add("Cookie", "age_check_done=1");

            // Add UserAgent
            if (!(request.Headers.UserAgent?.Count() > 0))
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.130 Safari/537.36");

            if (cfg.EnableJsProxy == false)
            {
                if (request.Headers.Referrer == null)
                    request.Headers.Referrer = request.RequestUri;

                return base.SendAsync(request, cancellationToken);
            }

            var jsproxy_url = cfg.JsProxy;
            // Add header to request here
            var url = request.RequestUri.ToString();
            var org_url = url;
            var i = org_url.IndexOf("/http/", StringComparison.CurrentCultureIgnoreCase);
            if (i > 0)
                org_url = org_url.Substring(i + 6);

            var uri_org = new Uri(org_url);
            var bypass = cfg.IsBypassed(uri_org.Host);

            if (bypass)
            {
                if (url != org_url)
                    request.RequestUri = new Uri(org_url);
            }
            else if (url.StartsWith(jsproxy_url, StringComparison.OrdinalIgnoreCase) != true)
            {
                url = $"{cfg.JsProxy.TrimEnd("/")}/http/{url}";
                request.RequestUri = new Uri(url);
            }

            url = request.Headers.Referrer?.ToString();
            if (string.IsNullOrWhiteSpace(url))
                request.Headers.Referrer = uri_org;

            return base.SendAsync(request, cancellationToken);
        }
    }
}