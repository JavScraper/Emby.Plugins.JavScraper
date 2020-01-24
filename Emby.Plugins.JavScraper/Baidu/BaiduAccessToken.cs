using System;

namespace Baidu.AI
{
    /// <summary>
    /// 百度人脸识别令牌
    /// </summary>
    public class BaiduAccessToken
    {
        /// <summary>
        /// Gets or sets the refresh token.
        /// </summary>
        public string refresh_token { get; set; }

        /// <summary>
        /// Access Token的有效期(秒为单位，一般为1个月)；
        /// </summary>
        public int expires_in { get; set; }

        /// <summary>
        /// Gets or sets the scope.
        /// </summary>
        public string scope { get; set; }

        /// <summary>
        /// Gets or sets the session key.
        /// </summary>
        public string session_key { get; set; }

        /// <summary>
        /// 要获取的Access Token
        /// </summary>
        public string access_token { get; set; }

        /// <summary>
        /// Gets or sets the session secret.
        /// </summary>
        public string session_secret { get; set; }

        /// <summary>
        /// 错误码；关于错误码的详细信息请参考下方鉴权认证错误码。
        /// </summary>
        public string error { get; set; }

        /// <summary>
        /// 错误描述信息，帮助理解和解决发生的错误。
        /// </summary>
        public string error_description { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime created { get; set; } = DateTime.Now;

        /// <summary>
        /// 过期时间
        /// </summary>
        public DateTime expired => created.AddSeconds(expires_in).AddHours(-1);

        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid => string.IsNullOrWhiteSpace(access_token) == false && expired > DateTime.Now;
    }
}