using LiteDB;
using System;

namespace Emby.Plugins.JavScraper.Data
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
        public string url { get; set; }

        /// <summary>
        /// 中心点位置
        /// </summary>
        public double point { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime created { get; set; }
    }
}
