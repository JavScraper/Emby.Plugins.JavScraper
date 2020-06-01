using System;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper.Http
{
    /// <summary>
    /// HttpClient
    /// </summary>
    public class HttpClientEx
    {
        /// <summary>
        /// 客户端初始话方法
        /// </summary>
        private readonly Action<HttpClient> ac;

        /// <summary>
        /// 当前客户端
        /// </summary>
        private HttpClient client = null;

        /// <summary>
        /// 配置版本号
        /// </summary>
        private long version = -1;

        /// <summary>
        /// 上一个客户端
        /// </summary>
        private HttpClient client_old = null;

        public HttpClientEx(Action<HttpClient> ac = null)
        {
            this.ac = ac;
        }

        /// <summary>
        /// 获取一个 HttpClient
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public HttpClient GetClient()
        {
            if (client != null && version == Plugin.Instance.Configuration.ConfigurationVersion)
                return client;

            if (client_old != null)
            {
                client_old.Dispose();
                client_old = null;
            }
            client_old = client;

            var handler = new ProxyHttpClientHandler();
            client = new HttpClient(handler, true);
            ac?.Invoke(client);

            return client;
        }

        public Task<string> GetStringAsync(string requestUri)
            => GetClient().GetStringAsync(requestUri);

        public Task<HttpResponseMessage> GetAsync(string requestUri)
            => GetClient().GetAsync(requestUri);

        public Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken)
            => GetClient().GetAsync(requestUri, cancellationToken);

        public Task<Stream> GetStreamAsync(string requestUri)
            => GetClient().GetStreamAsync(requestUri);

        public Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content)
            => GetClient().PostAsync(requestUri, content);

        public Uri BaseAddress => GetClient().BaseAddress;
    }
}