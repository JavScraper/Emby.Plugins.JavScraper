using Jellyfin.Plugin.JavScraper.Extensions;

namespace Jellyfin.Plugin.JavScraper.Scrapers.Model
{
    public class JavPersonIndex
    {
        /// <summary>
        /// 姓名
        /// </summary>
        public string Name { get; set; } = string.Empty;

        public string Overview { get; set; } = string.Empty;

        public string Avatar { get; set; } = string.Empty;

        /// <summary>
        /// 地址
        /// </summary>
        public string Url { get; set; } = string.Empty;

        public override string ToString() => this.ToJson();
    }
}
