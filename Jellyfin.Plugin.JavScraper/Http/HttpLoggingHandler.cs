using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Http
{
    public class HttpLoggingHandler : DelegatingHandler
    {
        private readonly ILogger _logger;

        public HttpLoggingHandler(HttpMessageHandler innerHandler, ILogger logger)
            : base(innerHandler)
        {
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if ((int)response.StatusCode > 399)
                {
                    LogRequestAndResponse(request, response, null, cancellationToken);
                }

                return response;
            }
            catch (Exception exception)
            {
                LogRequestAndResponse(request, response, exception, cancellationToken);
                throw;
            }
        }

        private async void LogRequestAndResponse(HttpRequestMessage? request, HttpResponseMessage? response, Exception? exception, CancellationToken cancellationToken)
        {
            if (request != null)
            {
                _logger.LogInformation("Request: {Request}", request);
            }

            if (request?.Content != null)
            {
                _logger.LogInformation("Content: {Content}", await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            }

            if (response != null)
            {
                _logger.LogInformation("Response: {Response}", response);
            }

            if (response?.Content != null)
            {
                _logger.LogInformation("Content: {Content}", await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            }
        }
    }
}
