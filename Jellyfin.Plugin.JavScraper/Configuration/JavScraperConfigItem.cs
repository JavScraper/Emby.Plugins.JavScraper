using System.Xml.Serialization;
using Jellyfin.Plugin.JavScraper.Extensions;

namespace Jellyfin.Plugin.JavScraper.Configuration
{
    /// <summary>
    /// 刮削器配置
    /// </summary>
    public class JavScraperConfigItem
    {
        /// <summary>
        /// 启用
        /// </summary>
        [XmlAttribute]
        public bool Enable { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        [XmlAttribute]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 地址
        /// </summary>
        [XmlAttribute]
        public string Url { get; set; } = string.Empty;

        public override string ToString() => this.ToJson();
    }
}
