using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JavScraper.Services.Model
{
    /// <summary>
    /// 翻译结果
    /// </summary>
    public class BaiduFanyiResult
    {
        /// <summary>
        /// 来源语言
        /// </summary>
        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;

        /// <summary>
        /// 目标语言
        /// </summary>
        [JsonPropertyName("to")]
        public string To { get; set; } = string.Empty;

        /// <summary>
        /// 翻译内容
        /// </summary>
        [JsonPropertyName("trans_result")]
        public IReadOnlyList<BaiduTranslateResult> TransResult { get; set; } = new List<BaiduTranslateResult>();

        public override string ToString() => System.Text.Json.JsonSerializer.Serialize(this);
    }
}
