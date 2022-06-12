using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using LiteDB;

namespace Jellyfin.Plugin.JavScraper.Data
{
    /// <summary>
    /// 影片情节信息
    /// </summary>
    public class Plot
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
        /// 去掉下划线和横线的番号
        /// </summary>
        [BsonField("num")]
        public string Num { get; set; } = string.Empty;

        /// <summary>
        /// 链接地址
        /// </summary>
        [BsonField("url")]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// 影片情节信息
        /// </summary>
        [BsonField("plot")]
        public string Info { get; set; } = string.Empty;

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
