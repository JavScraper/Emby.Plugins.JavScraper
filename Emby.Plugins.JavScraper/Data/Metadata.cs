using Emby.Plugins.JavScraper.Scrapers;
using LiteDB;
using System;

namespace Emby.Plugins.JavScraper.Data
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
        public ObjectId id { get; set; }

        /// <summary>
        /// 适配器
        /// </summary>
        public string provider { get; set; }

        /// <summary>
        /// 番号
        /// </summary>
        public string num { get; set; }

        /// <summary>
        /// 链接地址
        /// </summary>
        public string url { get; set; }

        /// <summary>
        /// 数据
        /// </summary>
        public JavVideo data { get; set; }

        /// <summary>
        /// 最后选中时间
        /// </summary>
        public DateTime? selected { get; set; }

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