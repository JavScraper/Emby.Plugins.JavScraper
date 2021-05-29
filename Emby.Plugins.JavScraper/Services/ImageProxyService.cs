﻿using Emby.Plugins.JavScraper.Data;
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
using MediaBrowser.Controller;
using System.Web;

namespace Emby.Plugins.JavScraper.Services
{
    /// <summary>
    /// 图片代理服务
    /// </summary>
    public class ImageProxyService
    {
        private HttpClientEx client;
        private static FileExtensionContentTypeProvider fileExtensionContentTypeProvider = new FileExtensionContentTypeProvider();

        public ImageProxyService(IServerApplicationHost serverApplicationHost, IJsonSerializer jsonSerializer, ILogger logger, IFileSystem fileSystem, IApplicationPaths appPaths)
        {
            client = new HttpClientEx();
            this.serverApplicationHost = serverApplicationHost;
            this.jsonSerializer = jsonSerializer;
            this.logger = logger;
            this.fileSystem = fileSystem;
            this.appPaths = appPaths;
        }

        private readonly IServerApplicationHost serverApplicationHost;
        private readonly IJsonSerializer jsonSerializer;
        private readonly ILogger logger;
        private readonly IFileSystem fileSystem;
        private readonly IApplicationPaths appPaths;

        /// <summary>
        /// 构造本地url地址
        /// </summary>
        /// <param name="url"></param>
        /// <param name="type"></param>
        /// <param name="with_api_url">是否包含 api url</param>
        /// <returns></returns>
        public async Task<string> GetLocalUrl(string url, ImageType type = ImageType.Backdrop, bool with_api_url = true)
        {
            if (string.IsNullOrEmpty(url))
                return url;

            if (url.IndexOf("Plugins/JavScraper/Image", StringComparison.OrdinalIgnoreCase) >= 0)
                return url;

            var api_url = with_api_url ? await serverApplicationHost.GetLocalApiUrl(default(CancellationToken)) : string.Empty;
            return $"{api_url}/emby/Plugins/JavScraper/Image?url={HttpUtility.UrlEncode(url)}&type={type}";
        }

        /// <summary>
        /// 获取图片
        /// </summary>
        /// <param name="url">地址</param>
        /// <param name="type">类型</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<HttpResponseInfo> GetImageResponse(string url, ImageType type, CancellationToken cancellationToken)
        {
            //  /emby/Plugins/JavScraper/Image?url=&type=xx
            if (url.IndexOf("Plugins/JavScraper/Image", StringComparison.OrdinalIgnoreCase) >= 0) //本地的链接
            {
                var uri = new Uri(url);
                var q = HttpUtility.ParseQueryString(uri.Query);
                var url2 = q["url"];
                if (url2.IsWebUrl())
                {
                    url = url2;
                    var tt = q.Get("type");
                    if (!string.IsNullOrWhiteSpace(tt) && Enum.TryParse<ImageType>(tt.Trim(), out var t2))
                        type = t2;
                }
            }

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
                        var ci = await CutImage(bytes, url);
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
                    var ci = await CutImage(await resp.Content.ReadAsByteArrayAsync(), url);
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
        /// <param name="url">图片地址</param>
        private async Task<HttpResponseInfo> CutImage(byte[] bytes, string url = null)
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
                                var x = await GetBaiduBodyAnalysisResult(bytes, url);
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
        /// <param name="url">图片地址</param>
        /// <returns></returns>
        private async Task<double> GetBaiduBodyAnalysisResult(byte[] bytes, string url)
        {
            var baidu = Plugin.Instance?.Configuration?.GetBodyAnalysisService(jsonSerializer);
            if (baidu == null)
                return 0;

            if (string.IsNullOrWhiteSpace(url) == false)
            {
                var p = Plugin.Instance.db.ImageFaceCenterPoints.FindById(url)?.point;
                if (p != null)
                    return p.Value;
            }
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
                    return Save(p.location.left * 2);

                //鼻子
                if (p.body_parts.nose?.x > 0)
                    return Save(p.body_parts.nose.x);
                //嘴巴
                if (p.body_parts.left_mouth_corner?.x > 0 && p.body_parts.right_mouth_corner.x > 0)
                    return Save((p.body_parts.left_mouth_corner.x + p.body_parts.right_mouth_corner.x) / 2);

                //头顶
                if (p.body_parts.top_head?.x > 0)
                    return Save(p.body_parts.top_head.x);
                //颈部
                if (p.body_parts.neck?.x > 0)
                    return Save(p.body_parts.neck.x);
            }
            catch { }

            double Save(double d)
            {
                if (string.IsNullOrWhiteSpace(url) == false)
                {
                    var item = new ImageFaceCenterPoint() { url = url, point = d, created = DateTime.Now };
                    Plugin.Instance.db.ImageFaceCenterPoints.Upsert(item);
                }
                return d;
            }

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