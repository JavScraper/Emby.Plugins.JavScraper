using System;
using LiteDB;

namespace Jellyfin.Plugin.JavScraper.Data
{
    /// <summary>
    /// 图片人脸中心点位置
    /// </summary>
    public class ImageFaceCenterPoint
    {
        /// <summary>
        /// url 地址
        /// </summary>
        [BsonId]
        [BsonField("url")]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// 中心点位置
        /// </summary>
        [BsonField("point")]
        public double Point { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [BsonField("created")]
        public DateTime Created { get; set; }

        public override string ToString() => System.Text.Json.JsonSerializer.Serialize(this);
    }
}
