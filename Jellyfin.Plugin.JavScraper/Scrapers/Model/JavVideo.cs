using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.JavScraper.Extensions;

namespace Jellyfin.Plugin.JavScraper.Scrapers.Model
{
    /// <summary>
    /// 视频
    /// </summary>
    public class JavVideo : JavVideoIndex
    {
        /// <summary>
        /// %genre:中文字幕?中文:%
        /// </summary>
        private static readonly Regex _regex_genre = new("%genre:(?<a>[^?]+)?(?<b>[^:]*):(?<c>[^%]*)%", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// 原始标题
        /// </summary>
        private string _originalTitle = string.Empty;

        /// <summary>
        /// 原始标题
        /// </summary>
        public string OriginalTitle
        {
            get => string.IsNullOrWhiteSpace(_originalTitle) ? (_originalTitle = Title) : _originalTitle;
            set => _originalTitle = value;
        }

        /// <summary>
        /// 内容简介
        /// </summary>
        public string Overview { get; set; } = string.Empty;

        /// <summary>
        /// 导演
        /// </summary>
        public string Director { get; set; } = string.Empty;

        /// <summary>
        /// 影片时长
        /// </summary>
        public string Runtime { get; set; } = string.Empty;

        /// <summary>
        /// 制作组
        /// </summary>
        public string Studio { get; set; } = string.Empty;

        /// <summary>
        /// 厂商
        /// </summary>
        public string Maker { get; set; } = string.Empty;

        /// <summary>
        /// 合集
        /// </summary>
        public string Set { get; set; } = string.Empty;

        /// <summary>
        /// 类别
        /// </summary>
        public IReadOnlyList<string> Genres { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 演员
        /// </summary>
        public IReadOnlyList<string> Actors { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 样品图片
        /// </summary>
        public IReadOnlyList<string> Samples { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 公众评分 0-10之间。
        /// </summary>
        public float? CommunityRating { get; set; }

        /// <summary>
        /// 获取格式化文件名
        /// </summary>
        /// <param name="format">格式化字符串</param>
        /// <param name="empty">空参数替代</param>
        /// <param name="clearInvalidPathChars">是否移除路径中的非法字符</param>
        /// <returns></returns>
        public string GetFormatName(string format, string empty, bool clearInvalidPathChars = false)
        {
            if (empty == null)
            {
                empty = string.Empty;
            }

            var m = this;
            void Replace(string key, string value)
            {
                var index = format.IndexOf(key, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    return;
                }

                if (string.IsNullOrEmpty(value))
                {
                    value = empty;
                }

                do
                {
                    format = format.Remove(index, key.Length);
                    format = format.Insert(index, value);
                    index = format.IndexOf(key, index + value.Length, StringComparison.OrdinalIgnoreCase);
                }
                while (index >= 0);
            }

            Replace("%num%", m.Num);
            Replace("%title%", m.Title);
            Replace("%title_original%", m.OriginalTitle);
            Replace("%actor%", string.Join(", ", m.Actors));
            Replace("%actor_first%", m.Actors.FirstOrDefault(string.Empty));
            Replace("%set%", m.Set);
            Replace("%director%", m.Director);
            Replace("%date%", m.Date);
            Replace("%year%", m.GetYear()?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            Replace("%month%", m.GetMonth()?.ToString("00", CultureInfo.InvariantCulture) ?? string.Empty);
            Replace("%studio%", m.Studio);
            Replace("%maker%", m.Maker);

            do
            {
                // %genre:中文字幕?中文:%
                var match = _regex_genre.Match(format);
                if (match.Success == false)
                {
                    break;
                }

                var a = match.Groups["a"].Value;
                var genre_key = m.Genres?.Contains(a, StringComparer.OrdinalIgnoreCase) == true ? "b" : "c";
                var genre_value = match.Groups[genre_key].Value;
                format = format.Replace(match.Value, genre_value, StringComparison.OrdinalIgnoreCase);
            }
            while (true);

            // 移除非法字符，以及修正路径分隔符
            if (clearInvalidPathChars)
            {
                format = string.Join(" ", format.Split(Path.GetInvalidPathChars()));
                if (Path.DirectorySeparatorChar == '/')
                {
                    format = format.Replace('\\', '/');
                }
                else if (Path.DirectorySeparatorChar == '\\')
                {
                    format = format.Replace('/', '\\');
                }
            }

            return format;
        }

        public override string ToString() => this.ToJson();
    }
}
