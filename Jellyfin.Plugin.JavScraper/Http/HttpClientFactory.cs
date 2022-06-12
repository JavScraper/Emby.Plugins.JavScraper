using System;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Http
{
    public interface ICustomHttpClientFactory
    {
        public HttpClient GetClient();
    }

    public sealed class HttpClientFactory : ICustomHttpClientFactory, IDisposable
    {
        private static readonly TimeSpan _expireAfter = TimeSpan.FromMinutes(2);
        private readonly IWebProxy _webProxy;
        private readonly ILoggerFactory _loggerFactory;
        private readonly object _lock = new();
        private DateTime _nextCreateTime = DateTime.Now;
        private HttpClient? _httpClient;
        private HttpMessageHandler? _httpMessageHandler;

        public HttpClientFactory(IWebProxy webProxy, ILoggerFactory loggerFactory)
        {
            _webProxy = webProxy;
            _loggerFactory = loggerFactory;
        }

        public HttpClient GetClient()
        {
            if (_httpClient == null || DateTime.Now > _nextCreateTime)
            {
                lock (_lock)
                {
                    if (_httpClient == null || DateTime.Now > _nextCreateTime)
                    {
                        if (_httpClient != null)
                        {
                            _httpClient.Dispose();
                            _httpMessageHandler?.Dispose();
                        }

                        _httpMessageHandler = new ProxyHttpClientHandler(_webProxy)
                        {
                            CheckCertificateRevocationList = true
                        };
                        // _httpMessageHandler = new HttpLoggingHandler(_httpMessageHandler, _loggerFactory);
                        _httpClient = new HttpClient(_httpMessageHandler);
                        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.130 Safari/537.36");
                        _nextCreateTime = DateTime.Now + _expireAfter;
                    }
                }
            }

            return _httpClient;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _httpMessageHandler?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
