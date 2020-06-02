using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper.Baidu
{
    /// <summary>
    /// 百度翻译
    /// </summary>
    public static class BaiduFanyiService
    {
        private static HttpClient client = new HttpClient() { BaseAddress = new Uri("http://api.fanyi.baidu.com/api/trans/vip/translate") };
        private static Random rd = new Random();

        public static async Task<BaiduFanyiResult> Fanyi(string q, IJsonSerializer jsonSerializer)
        {
            if (Plugin.Instance.Configuration.EnableBaiduFanyi == false)
                return null;

            // 源语言
            string from = "auto";
            // 目标语言
            string to = Plugin.Instance.Configuration.BaiduFanyiLanguage?.Trim();
            if (string.IsNullOrWhiteSpace(to))
                to = "zh";

            string appId = Plugin.Instance.Configuration.BaiduFanyiApiKey;
            string secretKey = Plugin.Instance.Configuration.BaiduFanyiSecretKey;

            string salt = rd.Next(100000).ToString();
            string sign = EncryptString(appId + q + salt + secretKey);

            var param = new Dictionary<string, string>()
            {
                ["q"] = q,
                ["from"] = from,
                ["to"] = to,
                ["appid"] = appId,
                ["salt"] = salt,
                ["sign"] = sign,
            };

            var resp = await client.PostAsync("", new FormUrlEncodedContent(param));

            if (resp.IsSuccessStatusCode == false)
                return null;

            var json = await resp.Content.ReadAsStringAsync();

            return jsonSerializer.DeserializeFromString<BaiduFanyiResult>(json);
        }

        /// <summary>
        /// 计算MD5值
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static string EncryptString(string str)
        {
            using (MD5 md5 = MD5.Create())
            {
                // 将字符串转换成字节数组
                byte[] byteOld = Encoding.UTF8.GetBytes(str);
                // 调用加密方法
                byte[] byteNew = md5.ComputeHash(byteOld);
                return BitConverter.ToString(byteNew).Replace("-", "").ToLower();
            }
        }
    }

    /// <summary>
    /// 翻译结果
    /// </summary>
    public class BaiduFanyiResult
    {
        /// <summary>
        /// 来源语言
        /// </summary>
        public string from { get; set; }

        /// <summary>
        /// 目标语言
        /// </summary>
        public string to { get; set; }

        /// <summary>
        /// 翻译内容
        /// </summary>
        public List<BaiduFanyiTransResult> trans_result { get; set; }
    }

    /// <summary>
    /// 翻译内容
    /// </summary>
    public class BaiduFanyiTransResult
    {
        public string src { get; set; }
        public string dst { get; set; }
    }
}