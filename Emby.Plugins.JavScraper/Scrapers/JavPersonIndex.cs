using MediaBrowser.Model.Entities;
using System.Collections.Generic;

namespace Emby.Plugins.JavScraper.Scrapers
{
    public class JavPersonIndex
    {
        /// <summary>
        /// 适配器
        /// </summary>
        public string Provider { get; set; }

        /// <summary>
        /// 姓名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 封面
        /// </summary>
        public string Cover { get; set; }

        /// <summary>
        /// 地址
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 图像类型
        /// </summary>
        public ImageType? ImageType { get; set; }

        /// <summary>
        /// 样品图片
        /// </summary>
        public List<string> Samples { get; set; }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
            => $"{Name}";
    }
}