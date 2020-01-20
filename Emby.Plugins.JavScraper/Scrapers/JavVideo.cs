using System.Collections.Generic;

namespace Emby.Plugins.JavScraper.Scrapers
{
    /// <summary>
    /// 视频
    /// </summary>
    public class JavVideo : JavVideoIndex
    {
        /// <summary>
        /// 内容简介
        /// </summary>
        public string Plot { get; set; }

        /// <summary>
        /// 导演
        /// </summary>
        public string Director { get; set; }

        /// <summary>
        /// 影片时长
        /// </summary>
        public string Runtime { get; set; }

        /// <summary>
        /// 制作组
        /// </summary>
        public string Studio { get; set; }

        /// <summary>
        /// 厂商
        /// </summary>
        public string Maker { get; set; }

        /// <summary>
        /// 合集
        /// </summary>
        public string Set { get; set; }

        /// <summary>
        /// 类别
        /// </summary>
        public List<string> Genres { get; set; }

        /// <summary>
        /// 演员
        /// </summary>
        public List<string> Actors { get; set; }

        /// <summary>
        /// 样品图片
        /// </summary>
        public List<string> Samples { get; set; }
    }
}