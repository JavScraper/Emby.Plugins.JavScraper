using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

#if __JELLYFIN__

using Microsoft.Extensions.Logging;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Model.Entities;

#else
using MediaBrowser.Model.Logging;
#endif

namespace Emby.Plugins.JavScraper.Services
{
#if !__JELLYFIN__
    /// <summary>
    /// 转发图片信息
    /// </summary>
    [Route("/emby/Plugins/JavScraper/Image", "GET")]
    public class GetImageInfo
    {
        /// <summary>
        /// 图像类型
        /// </summary>
        public ImageType? type { get; set; }

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
            => DoGet(request?.url, request?.type);

        /// <summary>
        /// 转发信息
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private async Task<object> DoGet(string url, ImageType? type)
        {
            logger.Info($"{url}");

            if (url.IsWebUrl() != true)
                throw new ResourceNotFoundException();

            var resp = await imageProxyService.GetImageResponse(url, type ?? ImageType.Backdrop, default);
            if (!(resp?.ContentLength > 0))
                throw new ResourceNotFoundException();

            return resultFactory.GetResult(Request, resp.Content, resp.ContentType);
        }
    }
#else

    [ApiController]
    [AllowAnonymous]
    [Route("/emby/Plugins/JavScraper/Image")]
    public class ImageService : ControllerBase
    {
        private readonly ILogger logger;

        public ImageService(ILoggerFactory logManager)
        {
            this.logger = logManager.CreateLogger<ImageService>();
        }

        /// <summary>
        /// 转发信息
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        [Route(""), HttpGet]
        public async Task<ActionResult> Get(string url, ImageType? type)
        {
            logger.Info($"{url}");

            if (url.IsWebUrl() != true)
                throw new ResourceNotFoundException();

            var imageProxyService = Plugin.Instance.ImageProxyService;

            var resp = await imageProxyService.GetImageResponse(url, type ?? ImageType.Backdrop, default);
            if (!(resp?.Content == null))
                return NoContent();

            return File(await resp.Content.ReadAsByteArrayAsync(), resp.Content.Headers.ContentType.ToString());
        }
    }

#endif
}