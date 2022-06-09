using System;
using System.Net.Http;

namespace Jellyfin.Plugin.JavScraper.Http
{
    public sealed class HttpClientFactory : IDisposable
    {
        /// <summary>
        /// 客户端初始话方法
        /// </summary>
        private readonly Action<HttpClient>? _action;
        private readonly object _lock = new();

        /// <summary>
        /// 当前客户端
        /// </summary>
        private HttpClient? _client;
        private HttpClientHandler? _httpClientHandler;

        /// <summary>
        /// 配置版本号
        /// </summary>
        private long _version = -1;

        public HttpClientFactory(Action<HttpClient>? ac = null)
        {
            this._action = ac;
        }

        /// <summary>
        /// 获取一个 HttpClient
        /// </summary>
        /// <returns></returns>
        public HttpClient GetClient()
        {
            if (_client == null || _version != Plugin.Instance.Configuration.ConfigurationVersion)
            {
                lock (_lock)
                {
                    var currentVersion = Plugin.Instance.Configuration.ConfigurationVersion;
                    if (_client == null || _version != currentVersion)
                    {
                        if (_client != null)
                        {
                            _client.Dispose();
                            _httpClientHandler?.Dispose();
                        }

                        _httpClientHandler = new ProxyHttpClientHandler();
                        _httpClientHandler.CheckCertificateRevocationList = true;
                        _client = new HttpClient(_httpClientHandler, true);
                        _action?.Invoke(_client);
                        _version = currentVersion;
                    }
                }
            }

            return _client;
        }

        public void Dispose()
        {
            _client?.Dispose();
            _httpClientHandler?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
