using System;

namespace Baidu.AI
{
    /// <summary>
    /// 结果
    /// </summary>
    /// <typeparam name="TData"></typeparam>
    public class BaiduApiResult<TData>
    {
        public int error_code { get; set; }
        public string error_msg { get; set; }
        public long log_id { get; set; }
        public int timestamp { get; set; }
        public int cached { get; set; }
        public TData result { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaiduApiResult{T}"/> class.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="success">if set to <c>true</c> [success].</param>
        public BaiduApiResult(TData result, bool success = true)
        {
            this.result = result;
            error_code = 0;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaiduApiResult{T}"/> class.
        /// </summary>
        public BaiduApiResult() { }

        /// <summary>
        /// 错误信息
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator string(BaiduApiResult<TData> model)
        {
            return model?.error_msg;
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
            return new BaiduApiResult<TData>() { error_msg = msg, error_code = 1 };
        }

        /// <summary>
        /// 内容信息
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator TData(BaiduApiResult<TData> model)
        {
            if (model == null)
                return default(TData);
            return model.result;
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
            return new BaiduApiResult<TData>() { result = t, error_code = 0 };
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
                error_msg = ex.Message,
                error_code = 1,
            };
        }
    }
}