using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JavScraper.Extensions;

namespace Jellyfin.Plugin.JavScraper.Http
{
    /// <summary>
    /// Proxy 客户端
    /// </summary>
    public class ProxyHttpClientHandler : HttpClientHandler
    {
        public ProxyHttpClientHandler(IWebProxy proxy)
        {
            // 忽略SSL证书问题
            ServerCertificateCustomValidationCallback = (message, certificate2, chain, errors) => true;
            Proxy = proxy;
            UseProxy = true;
        }

        /// <summary>
        /// 发送请求
        /// </summary>
        /// <param name="request">请求信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var cfg = Plugin.Instance.Configuration;

            request.Headers.Remove("X-FORWARDED-FOR");
            if (cfg.EnableXForwardedFor && !string.IsNullOrWhiteSpace(cfg.XForwardedFor) && IPAddress.TryParse(cfg.XForwardedFor, out var _))
            {
                request.Headers.TryAddWithoutValidation("X-FORWARDED-FOR", cfg.XForwardedFor);
            }

            if (request.RequestUri == null)
            {
                return base.SendAsync(request, cancellationToken);
            }

            if (!cfg.EnableJsProxy)
            {
                if (request.Headers.Referrer == null)
                {
                    request.Headers.Referrer = request.RequestUri;
                }
            }
            else
            {
                var url = request.RequestUri.ToString();
                var orgUrl = url;
                var i = orgUrl.IndexOf("/http/", StringComparison.CurrentCultureIgnoreCase);
                if (i > 0)
                {
                    orgUrl = orgUrl[(i + 6)..];
                }

                var orgUri = new Uri(orgUrl);

                if (cfg.IsBypassed(orgUri.Host))
                {
                    request.RequestUri = orgUri;
                }
                else
                {
                    request.RequestUri = new Uri($"{cfg.JsProxy.TrimEnd("/")}/http/{orgUrl}");
                }

                if (request.Headers.Referrer == null)
                {
                    request.Headers.Referrer = orgUri;
                }
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
