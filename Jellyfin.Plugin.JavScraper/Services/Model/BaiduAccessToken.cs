using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JavScraper.Services.Model
{
    /// <summary>
    /// 百度人脸识别令牌
    /// </summary>
    public class BaiduAccessToken
    {
        /// <summary>
        /// Gets or sets the refresh token.
        /// </summary>
        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        /// <summary>
        /// Access Token的有效期(秒为单位，一般为1个月)；
        /// </summary>
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        /// <summary>
        /// Gets or sets the scope.
        /// </summary>
        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        /// <summary>
        /// Gets or sets the session key.
        /// </summary>
        [JsonPropertyName("session_key")]
        public string? SessionKey { get; set; }

        /// <summary>
        /// 要获取的Access Token
        /// </summary>
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        /// <summary>
        /// Gets or sets the session secret.
        /// </summary>
        [JsonPropertyName("session_secret")]
        public string? SessionSecret { get; set; }

        /// <summary>
        /// 错误码；关于错误码的详细信息请参考下方鉴权认证错误码。
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        /// <summary>
        /// 错误描述信息，帮助理解和解决发生的错误。
        /// </summary>
        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [JsonPropertyName("created")]
        public DateTime Created { get; set; } = DateTime.Now;

        /// <summary>
        /// 过期时间
        /// </summary>
        [JsonPropertyName("expired")]
        public DateTime Expired => Created.AddSeconds(ExpiresIn).AddHours(-1);

        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(AccessToken) && Expired > DateTime.Now;
    }
}
