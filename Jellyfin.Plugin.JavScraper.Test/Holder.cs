using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Http;
using Jellyfin.Plugin.JavScraper.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Jellyfin.Plugin.JavScraper.Test
{
    internal class Holder
    {
        private static readonly object _locker = new();
        private static ILoggerFactory? _loggerFactory;
        private static HttpClient? _httpClient;
        private static IHttpClientManager? _httpClientFactory;
        private static ApplicationDatabase? _applicationDatabase;
        private static DMMService? _dmmService;


        public static ILoggerFactory GetLoggerFactory()
        {
            if (_loggerFactory == null)
            {
                _loggerFactory =  LoggerFactory.Create(builder =>
                    builder.AddSimpleConsole(options =>
                    {
                        options.IncludeScopes = true;
                        options.SingleLine = true;
                        options.TimestampFormat = "hh:mm:ss ";
                    }));
            }

            return _loggerFactory;
        }

        public static HttpClient GetHttpClient()
        {
            if (_httpClient == null)
            {
                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.130 Safari/537.36");
            }

            return _httpClient;
        }

        public static IHttpClientManager GetHttpClientManager()
        {
            if (_httpClientFactory == null)
            {
                var httpClientFactoryStub = new Mock<IHttpClientManager>();
                httpClientFactoryStub.Setup(x => x.GetClient()).Returns(GetHttpClient());
                _httpClientFactory = httpClientFactoryStub.Object;
            }

            return _httpClientFactory;
        }

        public static ApplicationDatabase GetApplicationDatabase()
        {
            if (_applicationDatabase == null)
            {
                lock (_locker)
                {
                    if (_applicationDatabase == null)
                    {
                        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "test.db");
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                        _applicationDatabase = new ApplicationDatabase("test.db");
                    }
                }
            }

            return _applicationDatabase;
        }

        public static DMMService GetDmmService()
        {
            if(_dmmService == null)
            {
                _dmmService = new DMMService(GetApplicationDatabase(), GetHttpClientManager());
            }

            return _dmmService;
        }
    }
}
