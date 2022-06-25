using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Polly;

namespace Jellyfin.Plugin.JavScraper.Http
{
    public class HttpRetryMessageHandler : DelegatingHandler
    {
        public HttpRetryMessageHandler(HttpMessageHandler innerHandler) : base(innerHandler)
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Policy
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(3, retryAttempt)))
                .ExecuteAsync(() => base.SendAsync(request, cancellationToken));
    }
}
