using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Net;

#if __JELLYFIN__

using Microsoft.Extensions.Logging;

#else
using MediaBrowser.Model.Logging;
#endif

using MediaBrowser.Model.Services;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using MihaZupan;
using Emby.Plugins.JavScraper.Scrapers;

namespace Emby.Plugins.JavScraper.Services
{
    /// <summary>
    /// 转发图片信息
    /// </summary>
    [Route("/emby/Plugins/JavScraper/Image", "GET")]
    public class GetImageInfo
    {
        /// <summary>
        /// 地址
        /// </summary>
        public string url { get; set; }
    }

    public class ImageService : IService, IRequiresRequest
    {
        private readonly IHttpResultFactory resultFactory;
        private readonly ILogger logger;
        private HttpClient client;

        /// <summary>
        /// Gets or sets the request context.
        /// </summary>
        /// <value>The request context.</value>
        public IRequest Request { get; set; }

        public ImageService(
#if __JELLYFIN__
            ILoggerFactory logManager
#else
            ILogManager logManager
#endif
            ,
            IHttpResultFactory resultFactory
            )
        {
            this.resultFactory = resultFactory;
            this.logger = logManager.CreateLogger<ImageService>();
            client = new HttpClient(ProxyHttpClientHandler.Instance, false);

            client.DefaultRequestHeaders.UserAgent.TryParseAdd($"JavScraper v{Assembly.GetExecutingAssembly().GetName().Version}");
        }

        public object Get(GetImageInfo request)
            => DoGet(request?.url);

        /// <summary>
        /// 转发信息
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private async Task<object> DoGet(string url)
        {
            logger.Info($"{url}");

            if (url.IsWebUrl() != true)
                throw new ResourceNotFoundException();

            var resp = await client.GetAsync(url);
            if (resp.IsSuccessStatusCode == false)
                throw new ResourceNotFoundException();

            return resultFactory.GetResult(Request, await resp.Content.ReadAsByteArrayAsync(), resp.Content.Headers.ContentType.ToString());
        }
    }
}