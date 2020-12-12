using Emby.Plugins.JavScraper.Http;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;

#if __JELLYFIN__
using Microsoft.Extensions.Logging;
#else
using MediaBrowser.Model.Logging;
#endif

using MediaBrowser.Model.Serialization;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper.Services
{
    /// <summary>
    /// 图片代理服务
    /// </summary>
    public class ImageProxyService
    {
        private HttpClientEx client;
        private static FileExtensionContentTypeProvider fileExtensionContentTypeProvider = new FileExtensionContentTypeProvider();

        public ImageProxyService(IJsonSerializer jsonSerializer, ILogger logger, IFileSystem fileSystem, IApplicationPaths appPaths)
        {
            client = new HttpClientEx();
            this.jsonSerializer = jsonSerializer;
            this.logger = logger;
            this.fileSystem = fileSystem;
            this.appPaths = appPaths;
        }

        private readonly IJsonSerializer jsonSerializer;
        private readonly ILogger logger;
        private readonly IFileSystem fileSystem;
        private readonly IApplicationPaths appPaths;

        public async Task<HttpResponseInfo> GetImageResponse(string url, ImageType type, CancellationToken cancellationToken)
        {
            logger?.Info($"{nameof(GetImageResponse)}-{url}");

            var key = WebUtility.UrlEncode(url);
            var cache_file = Path.Combine(appPaths.GetImageCachePath().ToString(), key);
            byte[] bytes = null;

            //尝试从缓存中读取
            try
            {
                var fi = fileSystem.GetFileInfo(cache_file);

                //图片文件存在，且是24小时之内的
                if (fi.Exists && fileSystem.GetFileInfo(cache_file).LastWriteTimeUtc > DateTime.Now.AddDays(-1).ToUniversalTime())
                {
                    bytes = await fileSystem.ReadAllBytesAsync(cache_file);
                    logger?.Info($"Hit image cache {url} {cache_file}");
                    if (type == ImageType.Primary)
                    {
                        var ci = await CutImage(bytes);
                        if (ci != null)
                            return ci;
                    }

                    fileExtensionContentTypeProvider.TryGetContentType(url, out var contentType);

                    return new HttpResponseInfo()
                    {
                        Content = new MemoryStream(bytes),
                        ContentLength = bytes.Length,
                        ContentType = contentType ?? "image/jpeg",
                        StatusCode = HttpStatusCode.OK,
                    };
                }
            }
            catch (Exception ex)
            {
                logger?.Warn($"Read image cache error. {url} {cache_file} {ex.Message}");
            }

            try
            {
                var resp = await client.GetAsync(url, cancellationToken);
                if (resp.IsSuccessStatusCode == false)
                    return await Parse(resp);

                try
                {
                    fileSystem.WriteAllBytes(cache_file, await resp.Content.ReadAsByteArrayAsync());
                    logger?.Info($"Save image cache {url} {cache_file} ");
                }
                catch (Exception ex)
                {
                    logger?.Warn($"Save image cache error. {url} {cache_file} {ex.Message}");
                }

                if (type == ImageType.Primary)
                {
                    var ci = await CutImage(await resp.Content.ReadAsByteArrayAsync());
                    if (ci != null)
                        return ci;
                }

                return await Parse(resp);
            }
            catch (Exception ex)
            {
                logger?.Error(ex.ToString());
            }
            return new HttpResponseInfo();
        }

        /// <summary>
        /// 剪裁图片
        /// </summary>
        /// <param name="bytes">图片内容</param>
        /// <returns>为空：剪裁失败或者不需要剪裁。</returns>
        private async Task<HttpResponseInfo> CutImage(byte[] bytes)
        {
            logger?.Info($"{nameof(CutImage)}: staring...");
            try
            {
                using (var ms = new MemoryStream(bytes))
                {
                    ms.Position = 0;
                    using (var inputStream = new SKManagedStream(ms))
                    {
                        using (var bitmap = SKBitmap.Decode(inputStream))
                        {
                            var h = bitmap.Height;
                            var w = bitmap.Width;
                            var w2 = h * 2 / 3; //封面宽度

                            if (w2 < w) //需要剪裁
                            {
                                var x = await GetBaiduBodyAnalysisResult(bytes);
                                var start_w = w - w2; //默认右边

                                if (x > 0) //百度人体识别，中心点位置
                                {
                                    if (x + w2 / 2 > w) //右边
                                        start_w = w - w2;
                                    else if (x - w2 / 2 < 0)//左边
                                        start_w = 0;
                                    else //居中
                                        start_w = (int)x - w2 / 2;
                                }

                                var image = SKImage.FromBitmap(bitmap);

                                var subset = image.Subset(SKRectI.Create(start_w, 0, w2, h));
                                var encodedData = subset.Encode(SKEncodedImageFormat.Jpeg, 90);
                                logger?.Info($"{nameof(CutImage)}: Already cut {w}x{h} --> start_w: {start_w}");
                                return new HttpResponseInfo()
                                {
                                    Content = encodedData.AsStream(),
                                    ContentLength = encodedData.Size,
                                    ContentType = "image/jpeg",
                                    StatusCode = HttpStatusCode.OK,
                                };
                            }

                            logger?.Info($"{nameof(CutImage)}: not need to cut. {w}x{h}");
                            return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Warn($"{nameof(CutImage)}: cut image failed. {ex.Message}");
            }
            logger?.Warn($"{nameof(CutImage)}: cut image failed.");
            return null;
        }

        /// <summary>
        /// 获取人脸的中间位置，
        /// </summary>
        /// <param name="bytes">图片数据</param>
        /// <returns></returns>
        private async Task<double> GetBaiduBodyAnalysisResult(byte[] bytes)
        {
            var baidu = Plugin.Instance?.Configuration?.GetBodyAnalysisService(jsonSerializer);
            if (baidu == null)
                return 0;

            try
            {
                var r = await baidu.BodyAnalysis(bytes);
                if (r?.person_info?.Any() != true)
                    return 0;

                //取面积最大的人
                var p = r.person_info.Where(o => o.location?.score >= 0.1).OrderByDescending(o => o.location?.width * o.location?.height).FirstOrDefault()
                    ?? r.person_info.FirstOrDefault();

                //人数大于15个，且有15个小于最大人脸，则直接用最右边的做封面。其实也可以考虑识别左边的条码，有条码直接取右边，但Emby中实现困难
                if (p != null && r.person_info.Where(o => o.location?.left < p.location.left).Count() > 15 && r.person_info.Where(o => o.location?.left > p.location.left).Count() < 10)
                    return p.location.left * 2;

                //鼻子
                if (p.body_parts.nose?.x > 0)
                    return p.body_parts.nose.x;
                //嘴巴
                if (p.body_parts.left_mouth_corner?.x > 0 && p.body_parts.right_mouth_corner.x > 0)
                    return (p.body_parts.left_mouth_corner.x + p.body_parts.right_mouth_corner.x) / 2;

                //头顶
                if (p.body_parts.top_head?.x > 0)
                    return p.body_parts.top_head.x;
                //颈部
                if (p.body_parts.neck?.x > 0)
                    return p.body_parts.neck.x;
            }
            catch { }

            return 0;
        }

        private async Task<HttpResponseInfo> Parse(HttpResponseMessage resp)
        {
            var r = new HttpResponseInfo()
            {
                Content = await resp.Content.ReadAsStreamAsync(),
                ContentLength = resp.Content.Headers.ContentLength,
                ContentType = resp.Content.Headers.ContentType?.ToString(),
                StatusCode = resp.StatusCode,
                Headers =
#if __JELLYFIN__
                resp.Headers
#else
                resp.Content.Headers.ToDictionary(o => o.Key, o => string.Join(", ", o.Value))
#endif
            };
            return r;
        }
    }
}