using System;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Http
{
    public interface IHttpClientManager
    {
        public HttpClient GetClient();
    }

    public sealed class HttpClientManager : IHttpClientManager, IDisposable
    {
        private readonly HttpMessageHandler _httpMessageHandler;
        private readonly object _lock = new();
        private volatile HttpClient? _httpClient;

        public HttpClientManager(IWebProxy webProxy, ILogger logger)
        {
            _httpMessageHandler = new ProxyHttpClientHandler(webProxy)
            {
                CheckCertificateRevocationList = true
            };
            _httpMessageHandler = new HttpLoggingHandler(_httpMessageHandler, logger);
            _httpMessageHandler = new HttpRetryMessageHandler(_httpMessageHandler);
        }

        public HttpClient GetClient()
        {
            if (_httpClient == null)
            {
                lock (_lock)
                {
                    if (_httpClient == null)
                    {
                        _httpClient = new HttpClient(_httpMessageHandler, false);
                        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Constants.UserAgent);
                        _httpClient.DefaultRequestHeaders.Connection.Add("keep-alive");
                    }
                }
            }

            return _httpClient;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _httpMessageHandler.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
