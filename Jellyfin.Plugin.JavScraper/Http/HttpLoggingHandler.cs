using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

// ReSharper disable TemplateIsNotCompileTimeConstantProblem

namespace Jellyfin.Plugin.JavScraper.Http
{
    [SuppressMessage("Usage", "CA2254:模板应为静态表达式", Justification = "<挂起>")]
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
                // Ignore dmm log for now
                if ((int)response.StatusCode > 399 && request.RequestUri?.ToString().Contains("www.dmm.co.jp", StringComparison.OrdinalIgnoreCase) == false)
                {
                    await LogRequestAndResponse(request, response, null, cancellationToken).ConfigureAwait(false);
                }

                return response;
            }
            catch (Exception exception)
            {
                await LogRequestAndResponse(request, response, exception, cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        private async Task LogRequestAndResponse(HttpRequestMessage? request, HttpResponseMessage? response, Exception? exception, CancellationToken cancellationToken)
        {
            var log = new StringBuilder();
            try
            {
                if (request != null)
                {
                    log.Append("Request: ").Append(request).Append('\n');
                }

                if (request?.Content != null)
                {
                    log.Append("Content: ").Append(await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).Append('\n');
                }

                if (response != null)
                {
                    log.Append("Response: ").Append(response).Append('\n');
                }

                if (response?.Content != null)
                {
                    log.Append("Content: ").Append(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).Append('\n');
                }
            }
            catch
            {
            }
            finally
            {
                if (exception == null)
                {
                    _logger.LogWarning(log.ToString());
                }
                else
                {
                    _logger.LogError(exception, log.ToString());
                }
            }
        }
    }
}
