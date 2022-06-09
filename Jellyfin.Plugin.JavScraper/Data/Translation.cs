using System;
using LiteDB;

namespace Jellyfin.Plugin.JavScraper.Data
{
    /// <summary>
    /// 翻译
    /// </summary>
    public class Translation
    {
        /// <summary>
        /// id
        /// </summary>
        [BsonId]
        [BsonField("id")]
        public ObjectId? Id { get; set; }

        /// <summary>
        /// 原始文本的MD5结果
        /// </summary>
        [BsonField("hash")]
        public string Hash { get; set; } = string.Empty;

        /// <summary>
        /// 目标语言
        /// </summary>
        [BsonField("lang")]
        public string Lang { get; set; } = string.Empty;

        /// <summary>
        /// 原始文本
        /// </summary>
        [BsonField("src")]
        public string Src { get; set; } = string.Empty;

        /// <summary>
        /// 翻译结果
        /// </summary>
        [BsonField("dst")]
        public string Dst { get; set; } = string.Empty;

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

        public override string ToString() => System.Text.Json.JsonSerializer.Serialize(this);
    }
}
