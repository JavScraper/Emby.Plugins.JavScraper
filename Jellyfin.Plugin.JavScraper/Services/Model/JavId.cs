using System.IO;

namespace Jellyfin.Plugin.JavScraper.Services.Model
{
    /// <summary>
    /// 类型
    /// </summary>
    public enum JavIdType
    {
        /// <summary>
        /// 不确定
        /// </summary>
        None,
        Censored,
        Uncensored,
        Suren
    }

    /// <summary>
    /// 番号
    /// </summary>
    public class JavId
    {
        /// <summary>
        /// 类型
        /// </summary>
        public JavIdType Type { get; set; } = JavIdType.None;

        /// <summary>
        /// 解析到的id
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 文件名
        /// </summary>
        public string File { get; set; } = string.Empty;

        /// <summary>
        /// 匹配器
        /// </summary>
        public string Matcher { get; set; } = string.Empty;

        /// <summary>
        /// 转换
        /// </summary>
        /// <param name="id"></param>
        public static implicit operator JavId(string id) => new() { Id = id };

        /// <summary>
        /// 转换
        /// </summary>
        /// <param name="id"></param>
        public static implicit operator string(JavId id) => id.Id;

        /// <summary>
        /// 识别
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns></returns>
        public static JavId? Parse(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var id = JavIdRecognizer.Parse(name);
            if (id != null)
            {
                id.File = path;
            }

            return id;
        }
    }
}
