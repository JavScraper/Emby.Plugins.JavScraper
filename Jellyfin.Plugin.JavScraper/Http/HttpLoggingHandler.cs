using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Http
{
    public class HttpLoggingHandler : DelegatingHandler
    {
        private readonly ILogger _logger;

        public HttpLoggingHandler(HttpMessageHandler innerHandler, ILoggerFactory loggerFactory)
            : base(innerHandler)
        {
            _logger = loggerFactory.CreateLogger<HttpLoggingHandler>();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Request: {Request}", request);
            if (request.Content != null)
            {
                _logger.LogInformation("Content: {Content}", await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            }

            HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Response: {Response}", response);
            if (response.Content != null)
            {
                _logger.LogInformation("Content: {Content}", await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            }

            return response;
        }
    }
}
