using System;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.JavScraper.Extensions;

namespace Jellyfin.Plugin.JavScraper.Services.Model
{
    /// <summary>
    /// 结果
    /// </summary>
    /// <typeparam name="TData"></typeparam>
    public class BaiduApiResult<TData>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BaiduApiResult{T}"/> class.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="success">if set to <c>true</c> [success].</param>
        public BaiduApiResult(TData result, bool success = true)
        {
            Result = result;
            ErrorCode = 0;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaiduApiResult{T}"/> class.
        /// </summary>
        public BaiduApiResult()
        {
        }

        [JsonPropertyName("error_code")]
        public int ErrorCode { get; set; }

        [JsonPropertyName("error_msg")]
        public string? ErrorMsg { get; set; }

        [JsonPropertyName("log_id")]
        public long LogId { get; set; }

        [JsonPropertyName("timestamp")]
        public int Timestamp { get; set; }

        [JsonPropertyName("cached")]
        public int Cached { get; set; }

        [JsonPropertyName("result")]
        public TData? Result { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator string(BaiduApiResult<TData> model)
        {
            return model?.ErrorMsg ?? string.Empty;
        }

        /// <summary>
        /// 内容信息
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator TData?(BaiduApiResult<TData> model)
        {
            if (model == null)
            {
                return default;
            }

            return model.Result;
        }

        /// <summary>
        /// 错误信息
        /// </summary>
        /// <param name="msg">The MSG.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator BaiduApiResult<TData>(string msg)
        {
            return new BaiduApiResult<TData>() { ErrorMsg = msg, ErrorCode = 1 };
        }

        /// <summary>
        /// 内容信息
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator BaiduApiResult<TData>(TData t)
        {
            return new BaiduApiResult<TData>() { Result = t, ErrorCode = 0 };
        }

        /// <summary>
        /// 模型校验错误信息
        /// </summary>
        /// <param name="ex">The ex.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator BaiduApiResult<TData>(Exception ex)
        {
            return new BaiduApiResult<TData>()
            {
                ErrorMsg = ex.Message,
                ErrorCode = 1,
            };
        }

        public override string ToString() => this.ToJson();
    }
}
