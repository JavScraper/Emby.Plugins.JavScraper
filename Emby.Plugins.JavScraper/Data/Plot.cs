using LiteDB;
using System;

namespace Emby.Plugins.JavScraper.Data
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
        public ObjectId id { get; set; }

        /// <summary>
        /// 适配器
        /// </summary>
        public string provider { get; set; }

        /// <summary>
        /// 去掉下划线和横线的番号
        /// </summary>
        public string num { get; set; }

        /// <summary>
        /// 链接地址
        /// </summary>
        public string url { get; set; }

        /// <summary>
        /// 简介
        /// </summary>
        public string plot { get; set; }

        /// <summary>
        /// 修改时间
        /// </summary>
        public DateTime modified { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime created { get; set; }
    }
}