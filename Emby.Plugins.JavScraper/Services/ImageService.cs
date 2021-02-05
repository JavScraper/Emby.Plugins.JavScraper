using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Net;

#if __JELLYFIN__
using Microsoft.Extensions.Logging;
#else
using MediaBrowser.Model.Logging;
#endif

using MediaBrowser.Model.Services;
using System.Threading.Tasks;
using MediaBrowser.Model.Entities;

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
        private readonly ImageProxyService imageProxyService;
        private readonly IHttpResultFactory resultFactory;
        private readonly ILogger logger;

        /// <summary>
        /// Gets or sets the request context.
        /// </summary>
        /// <value>The request context.</value>
        public IRequest Request { get; set; }

        public ImageService(
#if __JELLYFIN__
            ILoggerFactory logManager,
#else
            ILogManager logManager,
            ImageProxyService imageProxyService,
#endif
            IHttpResultFactory resultFactory
            )
        {
#if __JELLYFIN__
            imageProxyService = Plugin.Instance.ImageProxyService;
#else
            this.imageProxyService = imageProxyService;
#endif
            this.resultFactory = resultFactory;
            this.logger = logManager.CreateLogger<ImageService>();
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

            var resp = await imageProxyService.GetImageResponse(url, ImageType.Backdrop, default);
            if (!(resp?.ContentLength > 0))
                throw new ResourceNotFoundException();

            return resultFactory.GetResult(Request, resp.Content, resp.ContentType);
        }
    }
}