using System;
using System.Net;
using Jellyfin.Plugin.JavScraper.Configuration;
using MihaZupan;

namespace Jellyfin.Plugin.JavScraper.Http
{
    /// <summary>
    /// 代理服务器
    /// </summary>
    public sealed class JavWebProxy : IWebProxy, IDisposable
    {
        private IWebProxy? _proxy;

        public JavWebProxy()
        {
            Reset();
        }

        /// <summary>
        /// The credentials to submit to the proxy server for authentication.
        /// </summary>
        public ICredentials? Credentials
        {
            get => _proxy?.Credentials;
            set
            {
                if (_proxy != null)
                {
                    _proxy.Credentials = value;
                }
            }
        }

        /// <summary>
        /// 重设代理
        /// </summary>
        private void Reset()
        {
            var options = Plugin.Instance.Configuration;
            switch ((ProxyType)options.ProxyType)
            {
                case ProxyType.None:
                case ProxyType.JsProxy:
                default:
                    _proxy = null;
                    break;

                case ProxyType.HTTP:
                case ProxyType.HTTPS:
                case ProxyType.Socks5:
                    {
                        if (!string.IsNullOrWhiteSpace(options.ProxyHost) && options.ProxyPort > 0 && options.ProxyPort < 65535)
                        {
                            var hasCredential = !string.IsNullOrWhiteSpace(options.ProxyUserName) && !string.IsNullOrWhiteSpace(options.ProxyPassword);
                            if (options.ProxyType == (int)ProxyType.HTTP || options.ProxyType == (int)ProxyType.HTTPS)
                            {
                                var sm = options.ProxyType == (int)ProxyType.HTTP ? "http" : "https";
                                var url = $"{sm}://{options.ProxyHost}:{options.ProxyPort}";
                                _proxy = hasCredential ? new WebProxy(url, true, Array.Empty<string>(), new NetworkCredential() { UserName = options.ProxyUserName, Password = options.ProxyPassword }) : new WebProxy(url, true);
                            }
                            else
                            {
                                _proxy = hasCredential ? new HttpToSocks5Proxy(options.ProxyHost, options.ProxyPort, options.ProxyUserName, options.ProxyPassword) : new HttpToSocks5Proxy(options.ProxyHost, options.ProxyPort);
                            }
                        }

                        break;
                    }
            }
        }

        /// <summary>
        /// Returns the URI of a proxy.
        /// </summary>
        /// <param name="destination">A System.Uri that specifies the requested Internet resource.</param>
        /// <returns>A System.Uri instance that contains the URI of the proxy used to contact destination.</returns>
        public Uri? GetProxy(Uri destination)
            => _proxy?.GetProxy(destination);

        /// <summary>
        /// Indicates that the proxy should not be used for the specified host.
        /// </summary>
        /// <param name="host">The System.Uri of the host to check for proxy use.</param>
        /// <returns></returns>
        public bool IsBypassed(Uri host)
        {
            var options = Plugin.Instance.Configuration;
            return options.ProxyType == (int)ProxyType.None || options.EnableJsProxy || options.IsBypassed(host.Host) || _proxy == null || _proxy.IsBypassed(host);
        }

        public void Dispose()
        {
            if (_proxy is HttpToSocks5Proxy s5)
            {
                s5.StopInternalServer();
            }

            GC.SuppressFinalize(this);
        }
    }
}
