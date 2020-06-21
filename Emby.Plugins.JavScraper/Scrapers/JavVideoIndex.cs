using System;
using System.Globalization;

namespace Emby.Plugins.JavScraper.Scrapers
{
    /// <summary>
    /// 视频索引
    /// </summary>
    public class JavVideoIndex
    {
        /// <summary>
        /// 适配器
        /// </summary>
        public string Provider { get; set; }

        /// <summary>
        /// 地址
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 番号
        /// </summary>
        public string Num { get; set; }

        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 封面
        /// </summary>
        public string Cover { get; set; }

        /// <summary>
        /// 日期
        /// </summary>
        public string Date { get; set; }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
            => $"{Num} {Title}";

        /// <summary>
        /// 获取年份
        /// </summary>
        /// <returns></returns>
        public int? GetYear()
        {
            if (!(Date?.Length >= 4))
                return null;
            if (int.TryParse(Date.Substring(0, 4), out var y) && y > 0)
                return y;
            return null;
        }

        /// <summary>
        /// 获取月份
        /// </summary>
        /// <returns></returns>
        public int? GetMonth()
        {
            if (!(Date?.Length >= 6))
                return null;
            var d = Date.Split("-/ 年月日".ToCharArray());
            if (d.Length > 1)
            {
                if (int.TryParse(d[1], out var m) && m > 0 && m <= 12)
                    return m;
                return null;
            }
            if (int.TryParse(Date.Substring(4, 2), out var m2) && m2 > 0 && m2 <= 12)
                return m2;
            return null;
        }

#if __JELLYFIN__
        /// <summary>
        /// 获取日期
        /// </summary>
        /// <returns></returns>
        public DateTime? GetDate()
        {
            if (string.IsNullOrEmpty(Date))
                return null;
            if (DateTime.TryParseExact(Date, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime result6))
            {
                return result6.ToUniversalTime();
            }
            else if (DateTime.TryParse(Date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out result6))
            {
                return result6.ToUniversalTime();
            }
            return null;
        }
#else

        /// <summary>
        /// 获取日期
        /// </summary>
        /// <returns></returns>
        public DateTimeOffset? GetDate()
        {
            if (string.IsNullOrEmpty(Date))
                return null;
            if (DateTimeOffset.TryParseExact(Date, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTimeOffset result6))
            {
                return result6.ToUniversalTime();
            }
            else if (DateTimeOffset.TryParse(Date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out result6))
            {
                return result6.ToUniversalTime();
            }
            return null;
        }

#endif
    }
}