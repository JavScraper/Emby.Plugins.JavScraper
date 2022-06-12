using System;
using System.Net;
using Jellyfin.Plugin.JavScraper.Configuration;
using Microsoft.Extensions.Logging;
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
        public void Reset(PluginConfiguration config)
        {
            switch ((ProxyType)config.ProxyType)
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
                        if (!string.IsNullOrWhiteSpace(config.ProxyHost) && config.ProxyPort > 0 && config.ProxyPort < 65535)
                        {
                            var hasCredential = !string.IsNullOrWhiteSpace(config.ProxyUserName) && !string.IsNullOrWhiteSpace(config.ProxyPassword);
                            if (config.ProxyType == (int)ProxyType.HTTP || config.ProxyType == (int)ProxyType.HTTPS)
                            {
                                var sm = config.ProxyType == (int)ProxyType.HTTP ? "http" : "https";
                                var url = $"{sm}://{config.ProxyHost}:{config.ProxyPort}";
                                _proxy = hasCredential ? new WebProxy(url, true, Array.Empty<string>(), new NetworkCredential() { UserName = config.ProxyUserName, Password = config.ProxyPassword }) : new WebProxy(url, true);
                            }
                            else
                            {
                                _proxy = hasCredential ? new HttpToSocks5Proxy(config.ProxyHost, config.ProxyPort, config.ProxyUserName, config.ProxyPassword) : new HttpToSocks5Proxy(config.ProxyHost, config.ProxyPort);
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
