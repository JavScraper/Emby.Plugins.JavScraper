using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Http
{
    public interface IHttpClientManager
    {
        public HttpClient GetClient();
    }

    public sealed class HttpClientManager : IHttpClientManager, IDisposable
    {
        private static readonly TimeSpan _expireAfter = TimeSpan.FromMinutes(2);
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly HttpMessageHandler _httpMessageHandler;
        private readonly object _lock = new();
        private DateTime _nextCreateTime = DateTime.Now;
        private HttpClient? _httpClient;

        public HttpClientManager(IWebProxy webProxy, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<HttpClientManager>();
            _httpMessageHandler = new ProxyHttpClientHandler(webProxy)
            {
                CheckCertificateRevocationList = true
            };
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
                            var expiredHttpClient = _httpClient;
                            Task.Run(async () =>
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(1)).ConfigureAwait(false);
                                expiredHttpClient.Dispose();
                                _logger.LogInformation("Dipose expired http client.");
                            });
                        }

                        _logger.LogInformation("Create a new http client.");
                        // _httpMessageHandler = new HttpLoggingHandler(_httpMessageHandler, _loggerFactory);
                        _httpClient = new HttpClient(_httpMessageHandler, false);
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
