using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JavScraper.Scrapers.Model
{
    public class JavPersonIndex
    {
        /// <summary>
        /// 适配器
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// 姓名
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 封面
        /// </summary>
        public string Cover { get; set; } = string.Empty;

        /// <summary>
        /// 地址
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// 图像类型
        /// </summary>
        public ImageType? ImageType { get; set; }

        /// <summary>
        /// 样品图片
        /// </summary>
        public IReadOnlyList<string> Samples { get; set; } = System.Array.Empty<string>();

        /// <summary>
        /// 转换为字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString() => JsonSerializer.Serialize(this, new JsonSerializerOptions { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) });
    }
}
