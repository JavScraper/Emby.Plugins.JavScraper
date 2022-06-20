using System.Text.Json.Serialization;
using Jellyfin.Plugin.JavScraper.Extensions;

namespace Jellyfin.Plugin.JavScraper.Services.Model
{
    /// <summary>
    /// 翻译内容
    /// </summary>
    public class BaiduTranslateResult
    {
        [JsonPropertyName("src")]
        public string Src { get; set; } = string.Empty;

        [JsonPropertyName("dst")]
        public string Dst { get; set; } = string.Empty;

        public override string ToString() => this.ToJson();
    }
}
