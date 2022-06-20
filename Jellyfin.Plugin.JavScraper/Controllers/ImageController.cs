using System.Threading.Tasks;
using Jellyfin.Plugin.JavScraper.Extensions;
using Jellyfin.Plugin.JavScraper.Services;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("/Jellyfin/Plugins/JavScraper/Image")]
    public class ImageController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly ImageProxyService _imageProxyService;

        public ImageController(ILoggerFactory loggerFactory, ImageProxyService imageProxyService)
        {
            _logger = loggerFactory.CreateLogger<ImageController>();
            _imageProxyService = imageProxyService;
        }

        /// <summary>
        /// 转发信息
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        [Route("")]
        [HttpGet]
        public async Task<ActionResult> Get(string url, ImageType? type)
        {
            _logger.LogInformation("url={Url}", url);

            if (url.IsWebUrl() != true)
            {
                throw new ResourceNotFoundException();
            }

            var resp = await _imageProxyService.GetImageResponse(url, type ?? ImageType.Backdrop, default).ConfigureAwait(false);
            if (resp?.Content?.Headers?.ContentType == null)
            {
                return NoContent();
            }

            return File(await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false), resp.Content.Headers.ContentType.ToString());
        }
    }
}
