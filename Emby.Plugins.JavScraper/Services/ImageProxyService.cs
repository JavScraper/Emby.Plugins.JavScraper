using Emby.Plugins.JavScraper.Scrapers;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using SkiaSharp;
using System;
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
        private HttpClient client;

        public ImageProxyService(IJsonSerializer jsonSerializer, ILogger logger)
        {
            client = new HttpClient(new JsProxyHttpClientHandler(), true);
            this.jsonSerializer = jsonSerializer;
            this.logger = logger;
        }

        private const string image_type_param_name = "__image_type";
        private readonly IJsonSerializer jsonSerializer;
        private readonly ILogger logger;

        /// <summary>
        /// 构造图片代理地址
        /// </summary>
        /// <param name="url"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string BuildUrl(string url, int type)
        {
            if (type != 1)
                return url;

            var sp = url.IndexOf('?') > 0 ? "&" : "?";
            return $"{url}{sp}{image_type_param_name}={type}";
        }

        public async Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            logger?.Info($"{nameof(ImageProxyService)}-{nameof(GetImageResponse)}-{url}");
            int type = 0;
            var i = url.IndexOf(image_type_param_name, StringComparison.OrdinalIgnoreCase);
            if (i > 0)
            {
                try
                {
                    var p = url.Substring(i + image_type_param_name.Length).Trim('=').Trim();
                    url = url.Substring(0, i - 1);//减去一个连接符
                    int.TryParse(p, out type);
                }
                catch (Exception ex)
                {
                    logger?.Error(ex.ToString());
                }
            }

            try
            {
                var resp = await client.GetAsync(url, cancellationToken);
                if (resp.IsSuccessStatusCode == false)
                    return await Parse(resp);

                if (type == 1)
                {
                    try
                    {
                        using (var inputStream = new SKManagedStream(await resp.Content.ReadAsStreamAsync()))
                        {
                            using (var bitmap = SKBitmap.Decode(inputStream))
                            {
                                var h = bitmap.Height;
                                var w = bitmap.Width;
                                var w2 = h * 2 / 3; //封面宽度

                                if (w2 < w) //需要剪裁
                                {
                                    var x = await GetBaiduBodyAnalysisResult(resp);
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
                                    var encodedData = subset.Encode(SKEncodedImageFormat.Png, 75);

                                    return new HttpResponseInfo()
                                    {
                                        Content = encodedData.AsStream(),
                                        ContentLength = encodedData.Size,
                                        ContentType = "image/png",
                                        StatusCode = HttpStatusCode.OK,
                                    };
                                }
                            }
                        }
                    }
                    catch { }
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
        /// 获取人脸的中间位置
        /// </summary>
        /// <param name="resp"></param>
        /// <returns></returns>
        private async Task<double> GetBaiduBodyAnalysisResult(HttpResponseMessage resp)
        {
            var baidu = Plugin.Instance?.Configuration?.GetBodyAnalysisService(jsonSerializer);
            if (baidu == null)
                return 0;

            try
            {
                var r = await baidu.BodyAnalysis(await resp.Content.ReadAsByteArrayAsync());
                if (r?.person_info?.Any() != true)
                    return 0;
                //取面积最大的人
                var p = r.person_info.OrderByDescending(o => o.location?.width * o.location?.height).FirstOrDefault();
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
                Headers = resp.Content.Headers.ToDictionary(o => o.Key, o => string.Join(", ", o.Value))
            };
            return r;
        }
    }
}