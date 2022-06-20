using System;
using System.Globalization;
using Jellyfin.Plugin.JavScraper.Extensions;

namespace Jellyfin.Plugin.JavScraper.Scrapers.Model
{
    /// <summary>
    /// 视频索引
    /// </summary>
    public class JavVideoIndex
    {
        /// <summary>
        /// 适配器
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// 地址
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// 番号
        /// </summary>
        public string Num { get; set; } = string.Empty;

        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 封面
        /// </summary>
        public string Cover { get; set; } = string.Empty;

        /// <summary>
        /// 日期
        /// </summary>
        public string Date { get; set; } = string.Empty;

        /// <summary>
        /// 转换为字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString() => this.ToJson();

        /// <summary>
        /// 获取年份
        /// </summary>
        /// <returns></returns>
        public int? GetYear()
        {
            if (!(Date?.Length >= 4))
            {
                return null;
            }

            if (int.TryParse(Date.AsSpan(0, 4), out var y) && y > 0)
            {
                return y;
            }

            return null;
        }

        /// <summary>
        /// 获取月份
        /// </summary>
        /// <returns></returns>
        public int? GetMonth()
        {
            if (!(Date?.Length >= 6))
            {
                return null;
            }

            var d = Date.Split("-/ 年月日".ToCharArray());
            if (d.Length > 1)
            {
                if (int.TryParse(d[1], out var m) && m > 0 && m <= 12)
                {
                    return m;
                }

                return null;
            }

            if (int.TryParse(Date.AsSpan(4, 2), out var m2) && m2 > 0 && m2 <= 12)
            {
                return m2;
            }

            return null;
        }

        /// <summary>
        /// 获取日期
        /// </summary>
        /// <returns></returns>
        public DateTime? GetDateTime()
        {
            if (string.IsNullOrEmpty(Date))
            {
                return null;
            }

            if (DateTime.TryParseExact(Date, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var result))
            {
                return result.ToUniversalTime();
            }
            else if (DateTime.TryParse(Date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out result))
            {
                return result.ToUniversalTime();
            }

            return null;
        }
    }
}
