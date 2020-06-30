using LiteDB;
using System;
using System.Security.Cryptography;
using System.Text;

namespace Emby.Plugins.JavScraper.Data
{
    /// <summary>
    /// 翻译
    /// </summary>
    public class Translation
    {
        /// <summary>
        /// id
        /// </summary>
        [BsonId]
        public ObjectId id { get; set; }

        /// <summary>
        /// 原始文本的MD5结果
        /// </summary>
        public string hash { get; set; }

        /// <summary>
        /// 目标语言
        /// </summary>
        public string lang { get; set; }

        /// <summary>
        /// 原始文本
        /// </summary>
        public string src { get; set; }

        /// <summary>
        /// 翻译结果
        /// </summary>
        public string dst { get; set; }


        /// <summary>
        /// 修改时间
        /// </summary>
        public DateTime modified { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime created { get; set; }

        /// <summary>
        /// 计算原始文本的 Hash
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        public static string CalcHash(string src)
        {
            if (string.IsNullOrWhiteSpace(src))
                return string.Empty;

            using (var md5 = MD5.Create())
            {
                var result = md5.ComputeHash(Encoding.UTF8.GetBytes(src));
                var strResult = BitConverter.ToString(result);
                return strResult.Replace("-", "").ToLower();
            }
        }
    }
}