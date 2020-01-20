using Emby.Plugins.JavScraper.Scrapers;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
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

        public ImageProxyService(ILogger logger)
        {
            client = new HttpClient(new JsProxyHttpClientHandler(), true);
            this.logger = logger;
        }

        private const string image_type_param_name = "__image_type";
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
                                var w2 = h * 2 / 3;

                                if (w2 < w)
                                {
                                    var image = SKImage.FromBitmap(bitmap);
                                    var start_w = w - w2;
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