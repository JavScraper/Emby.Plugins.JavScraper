using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Jellyfin.Plugin.JavScraper.Scrapers.Model;
using LiteDB;

namespace Jellyfin.Plugin.JavScraper.Data
{
    /// <summary>
    /// 影片元数据
    /// </summary>
    public class Metadata
    {
        /// <summary>
        /// id
        /// </summary>
        [BsonId]
        [BsonField("id")]
        public ObjectId? Id { get; set; }

        /// <summary>
        /// 适配器
        /// </summary>
        [BsonField("provider")]
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// 番号
        /// </summary>
        [BsonField("num")]
        public string Num { get; set; } = string.Empty;

        /// <summary>
        /// 链接地址
        /// </summary>
        [BsonField("url")]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// 数据
        /// </summary>
        [BsonField("data")]
        public JavVideo Data { get; set; } = new();

        /// <summary>
        /// 最后选中时间
        /// </summary>
        [BsonField("selected")]
        public DateTime? Selected { get; set; }

        /// <summary>
        /// 修改时间
        /// </summary>
        [BsonField("modified")]
        public DateTime Modified { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [BsonField("created")]
        public DateTime Created { get; set; }

        public override string ToString() => System.Text.Json.JsonSerializer.Serialize(this, new JsonSerializerOptions { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) });
    }
}
