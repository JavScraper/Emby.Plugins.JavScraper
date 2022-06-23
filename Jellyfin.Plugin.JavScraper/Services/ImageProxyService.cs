using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Extensions;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.JavScraper.Services
{
    /// <summary>
    /// 图片代理服务
    /// </summary>
    public sealed class ImageProxyService
    {
        private readonly IServerApplicationHost _serverApplicationHost;
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;
        private readonly IApplicationPaths _appPaths;
        private readonly IHttpClientFactory _clientFactory;
        private readonly ApplicationDatabase _applicationDatabase;
        private readonly BodyAnalysisService _bodyAnalysisService;

        public ImageProxyService(
            IServerApplicationHost serverApplicationHost,
            ILoggerFactory loggerFactory,
            IFileSystem fileSystem,
            IApplicationPaths appPaths,
            ApplicationDatabase applicationDatabase,
            BodyAnalysisService bodyAnalysisService,
            IHttpClientFactory clientFactory)
        {
            _serverApplicationHost = serverApplicationHost;
            _logger = loggerFactory.CreateLogger<ImageProxyService>();
            _fileSystem = fileSystem;
            _applicationDatabase = applicationDatabase;
            _appPaths = appPaths;
            _bodyAnalysisService = bodyAnalysisService;
            _clientFactory = clientFactory;
        }

        /// <summary>
        /// 构造本地url地址
        /// </summary>
        /// <param name="url"></param>
        /// <param name="type"></param>
        /// <param name="withApiUrl">是否包含 api url</param>
        /// <returns></returns>
        public string GetLocalUrl(string url, ImageType type = ImageType.Backdrop, bool withApiUrl = true)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return url;
            }

            if (url.Contains("Plugins/JavScraper/Image", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            var apiUrl = withApiUrl ? _serverApplicationHost.GetApiUrlForLocalAccess() : string.Empty;
            return $"{apiUrl}/Jellyfin/Plugins/JavScraper/Image?url={HttpUtility.UrlEncode(url)}&type={type}";
        }

        /// <summary>
        /// 获取图片
        /// </summary>
        /// <param name="uriString">地址</param>
        /// <param name="type">类型</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> GetImageResponse(string uriString, ImageType type, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(uriString))
            {
                throw new ArgumentException($"{nameof(uriString)} can not be null or space", nameof(uriString));
            }

            // /Jellyfin/Plugins/JavScraper/Image?url=&type=xx
            if (uriString.Contains("Plugins/JavScraper/Image", StringComparison.OrdinalIgnoreCase)) // 本地的链接
            {
                var uri = new Uri(uriString);
                var query = HttpUtility.ParseQueryString(uri.Query);
                var urlString = query["url"] ?? string.Empty;
                if (urlString.IsWebUrl())
                {
                    uriString = urlString;
                    if (Enum.TryParse<ImageType>(query.Get("type")?.Trim(), out var t2))
                    {
                        type = t2;
                    }
                }
            }

            _logger.LogInformation("{Method}-{Uri}-{Type}", nameof(GetImageResponse), uriString, type);

            var key = WebUtility.UrlEncode(uriString);
            var cacheDirectory = _appPaths.ImageCachePath;
            Directory.CreateDirectory(cacheDirectory);

            var cacheFilePath = Path.Combine(cacheDirectory, key);

            // 尝试从缓存中读取
            try
            {
                if (cacheFilePath.Contains("../", StringComparison.Ordinal) || cacheFilePath.Length > 256)
                {
                    throw new ArgumentException(nameof(key));
                }
#pragma warning disable CA3003
                var cacheFile = _fileSystem.GetFileInfo(cacheFilePath);
#pragma warning disable CA3003
                // 图片文件存在，且是24小时之内的
                if (cacheFile.Exists && cacheFile.LastWriteTimeUtc > DateTime.Now.AddDays(-1).ToUniversalTime())
                {
                    var bytes = await File.ReadAllBytesAsync(cacheFilePath, CancellationToken.None).ConfigureAwait(false);
                    _logger.LogInformation("Hit image cache {Uri} {File}", $"{nameof(uriString)}={uriString}", $"{nameof(cacheFilePath)}={cacheFilePath}");
                    if (type == ImageType.Primary)
                    {
                        var ci = await CutImage(bytes, uriString).ConfigureAwait(false);
                        if (ci != null)
                        {
                            return ci;
                        }
                    }

                    if (FileExtensionContentTypeProvider.TryGetContentType(uriString, out var contentType))
                    {
                        return CreateHttpResponseInfo(bytes, contentType);
                    }
                    else
                    {
                        return CreateHttpResponseInfo(bytes);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Read image cache error. {Uri} {File}", $"{nameof(uriString)}={uriString}", $"{nameof(cacheFilePath)}={cacheFilePath}");
            }

            try
            {
                var resp = await _clientFactory.CreateClient().GetAsync(uriString, cancellationToken).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    return resp;
                }

                var bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                await File.WriteAllBytesAsync(cacheFilePath, bytes, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Save image cache uriString={Uri} cacheFilePath={File}", uriString, cacheFilePath);

                if (type == ImageType.Primary)
                {
                    var ci = await CutImage(bytes, uriString).ConfigureAwait(false);
                    if (ci != null)
                    {
                        return ci;
                    }
                }

                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Save image cache error. uriString={Uri} cacheFilePath={File}", uriString, cacheFilePath);
            }

            return new HttpResponseMessage();
        }

        /// <summary>
        /// 剪裁图片
        /// </summary>
        /// <param name="bytes">图片内容</param>
        /// <returns>为空：剪裁失败或者不需要剪裁。</returns>
        /// <param name="url">图片地址</param>
        private async Task<HttpResponseMessage?> CutImage(byte[] bytes, string url)
        {
            _logger.LogInformation($"{nameof(CutImage)}: staring...");
            try
            {
                using var ms = new MemoryStream(bytes);
                ms.Position = 0;
                using var inputStream = new SKManagedStream(ms);
                using var bitmap = SKBitmap.Decode(inputStream);
                var h = bitmap.Height;
                var w = bitmap.Width;
                var w2 = h * 2 / 3; // 封面宽度

                if (w2 < w) // 需要剪裁
                {
                    var x = await GetBaiduBodyAnalysisResult(bytes, url).ConfigureAwait(false);
                    var start_w = w - w2; // 默认右边

                    if (x > 0) // 百度人体识别，中心点位置
                    {
                        if (x + (w2 / 2.0) > w) // 右边
                        {
                            start_w = w - w2;
                        }
                        else if (x - (w2 / 2.0) < 0)// 左边
                        {
                            start_w = 0;
                        }
                        else // 居中
                        {
                            start_w = (int)x - (w2 / 2);
                        }
                    }

                    var image = SKImage.FromBitmap(bitmap);

                    var subset = image.Subset(SKRectI.Create(start_w, 0, w2, h));
                    var encodedData = subset.Encode(SKEncodedImageFormat.Jpeg, 90);
                    _logger.LogInformation("{Method}: Already cut {Width}*{Height} --> start_w: {Start}", nameof(CutImage), w, h, start_w);
                    return CreateHttpResponseInfo(encodedData.ToArray());
                }

                _logger.LogInformation("{Method}: not need to cut. {Width}*{Height}", nameof(CutImage), w, h);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Method}: cut image failed.", nameof(CutImage));
            }

            _logger.LogWarning($"{nameof(CutImage)}: cut image failed.");
            return null;
        }

        /// <summary>
        /// 获取人脸的中间位置，
        /// </summary>
        /// <param name="bytes">图片数据</param>
        /// <param name="url">图片地址</param>
        /// <returns></returns>
        private async Task<double> GetBaiduBodyAnalysisResult(byte[] bytes, string url)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                var p = _applicationDatabase.ImageFaceCenterPoints.FindById(url)?.Point;
                if (p != null)
                {
                    return p.Value;
                }
            }

            try
            {
                var result = await _bodyAnalysisService.BodyAnalysis(bytes).ConfigureAwait(false);
                var personInfos = result?.PersonInfos;
                if (personInfos == null)
                {
                    return 0;
                }

                // 取面积最大的人
                var personInfo = personInfos.Where(o => o.Location.Score >= 0.1).OrderByDescending(o => o.Location.Width * o.Location.Height).FirstOrDefault()
                    ?? personInfos.FirstOrDefault();

                if (personInfo == null)
                {
                    return 0;
                }

                // 人数大于15个，且有15个小于最大人脸，则直接用最右边的做封面。其实也可以考虑识别左边的条码，有条码直接取右边，但Jellyfin中实现困难
                if (personInfos.Count(o => o.Location.Left < personInfo.Location.Left) > 15 && personInfos.Count(o => o.Location.Left > personInfo.Location.Left) < 10)
                {
                    return Save(personInfo.Location.Left * 2);
                }

                // 头顶
                if (personInfo.BodyParts.TopHead?.X > 0)
                {
                    return Save(personInfo.BodyParts.TopHead.X);
                }

                // 颈部
                if (personInfo.BodyParts.Neck?.X > 0)
                {
                    return Save(personInfo.BodyParts.Neck.X);
                }

                // 鼻子
                if (personInfo.BodyParts.Nose?.X > 0)
                {
                    return Save(personInfo.BodyParts.Nose.X);
                }

                // 嘴巴
                if (personInfo.BodyParts.LeftMouthCorner?.X > 0 && personInfo.BodyParts?.RightMouthCorner?.X > 0)
                {
                    return Save((personInfo.BodyParts.LeftMouthCorner.X + personInfo.BodyParts.RightMouthCorner.X) / 2);
                }
            }
            catch
            {
            }

            double Save(double d)
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    var item = new ImageFaceCenterPoint() { Url = url, Point = d, Created = DateTime.Now };
                    _applicationDatabase.ImageFaceCenterPoints.Upsert(item);
                }

                return d;
            }

            return 0;
        }

        public static HttpResponseMessage CreateHttpResponseInfo(byte[] bytes, string contentType = "image/jpeg")
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

            return response;
        }
    }
}
