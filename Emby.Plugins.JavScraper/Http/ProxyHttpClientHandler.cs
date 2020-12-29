using Emby.Plugins.JavScraper.Configuration;
using MihaZupan;
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
            var cfg = Plugin.Instance.Configuration;

            var type = (ProxyTypeEnum)(cfg?.ProxyType ?? -1);
            switch (type)
            {
                case ProxyTypeEnum.None:
                case ProxyTypeEnum.JsProxy:
                default:
                    Proxy = null;
                    break;

                case ProxyTypeEnum.HTTP:
                case ProxyTypeEnum.HTTPS:
                case ProxyTypeEnum.Socks5:
                    {
                        IWebProxy p = null;
                        if (string.IsNullOrWhiteSpace(cfg?.ProxyHost) == false && cfg?.ProxyPort > 0 && cfg?.ProxyPort < 65535)
                        {
                            var hasCredential = string.IsNullOrWhiteSpace(cfg.ProxyUserName) == false && string.IsNullOrWhiteSpace(cfg.ProxyPassword) == false;
                            if (type == ProxyTypeEnum.HTTP || type == ProxyTypeEnum.HTTPS)
                            {
                                var sm = type == ProxyTypeEnum.HTTP ? "http" : "https";
                                var url = $"{sm}://{cfg.ProxyHost}:{cfg.ProxyPort}";
                                p = hasCredential == false ? new WebProxy(url, true) :
                                    new WebProxy(url, true, new string[] { }, new NetworkCredential() { UserName = cfg.ProxyUserName, Password = cfg.ProxyPassword });
                                ;
                            }
                            else
                                p = hasCredential == false ? new HttpToSocks5Proxy(cfg.ProxyHost, cfg.ProxyPort) :
                                     new HttpToSocks5Proxy(cfg.ProxyHost, cfg.ProxyPort, cfg.ProxyUserName, cfg.ProxyPassword);
                        }
                        Proxy = p;
                        break;
                    }
            }
            UseProxy = Proxy != null;
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
            request.Headers.Remove("X-FORWARDED-FOR");
            if (Plugin.Instance.Configuration.EnableX_FORWARDED_FOR && !string.IsNullOrWhiteSpace(Plugin.Instance.Configuration.X_FORWARDED_FOR) &&
                IPAddress.TryParse(Plugin.Instance.Configuration.X_FORWARDED_FOR, out var _))
                request.Headers.TryAddWithoutValidation("X-FORWARDED-FOR", Plugin.Instance.Configuration.X_FORWARDED_FOR);

            //mgstage.com 加入年龄认证Cookies
            if (request.RequestUri.ToString().Contains("mgstage.com") && !(request.Headers.TryGetValues("Cookie", out var cookies) && cookies.Contains("abc=1")))
                request.Headers.Add("Cookie", "adc=1");

            // Add UserAgent
            if (!(request.Headers.UserAgent?.Count() > 0))
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.130 Safari/537.36");

            if (Plugin.Instance.Configuration.EnableJsProxy == false)
            {
                if (request.Headers.Referrer == null)
                    request.Headers.Referrer = request.RequestUri;

                return base.SendAsync(request, cancellationToken);
            }

            var jsproxy_url = Plugin.Instance.Configuration.JsProxy;
            // Add header to request here
            var url = request.RequestUri.ToString();
            var org_url = url;
            var i = org_url.IndexOf("/http/", StringComparison.CurrentCultureIgnoreCase);
            if (i > 0)
                org_url = org_url.Substring(i + 6);

            var uri_org = new Uri(org_url);
            var bypass = Plugin.Instance.Configuration.IsJsProxyBypass(uri_org.Host);

            if (bypass)
            {
                if (url != org_url)
                    request.RequestUri = new Uri(org_url);
            }
            else if (url.StartsWith(jsproxy_url, StringComparison.OrdinalIgnoreCase) != true)
            {
                url = BuildJsProxyUrl(url);
                request.RequestUri = new Uri(url);
            }

            url = request.Headers.Referrer?.ToString();
            if (string.IsNullOrWhiteSpace(url))
                request.Headers.Referrer = uri_org;

            return base.SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// 构造代理地址
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private string BuildJsProxyUrl(string url)
            => string.IsNullOrWhiteSpace(url) == false && Plugin.Instance.Configuration.IsJsProxyBypass(GetHost(url)) == false ? $"{Plugin.Instance.Configuration.JsProxy.TrimEnd("/")}/http/{url}" : url;

        /// <summary>
        /// 获取域名
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private string GetHost(string url)
        {
            try
            {
                return new Uri(url).Host;
            }
            catch { }

            return url;
        }
    }
}