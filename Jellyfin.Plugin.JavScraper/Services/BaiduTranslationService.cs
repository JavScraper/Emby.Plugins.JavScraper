using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.JavScraper;
using Jellyfin.Plugin.JavScraper.Services.Model;

namespace Jellyfin.Plugin.JavScraper.Services
{
    /// <summary>
    /// 百度翻译
    /// </summary>
    public static class BaiduTranslationService
    {
        private static readonly HttpClient _client = new() { BaseAddress = new Uri("http://api.fanyi.baidu.com/api/trans/vip/translate") };
        private static readonly Random _random = new();

        public static async Task<BaiduFanyiResult?> Translate(string q)
        {
            if (!Plugin.Instance.Configuration.EnableBaiduFanyi || Plugin.Instance.Configuration.BaiduFanyiApiKey == null)
            {
                return null;
            }

            // 源语言
            var from = "auto";
            // 目标语言
            var to = Plugin.Instance.Configuration.BaiduFanyiLanguage.Trim();
            if (string.IsNullOrWhiteSpace(to))
            {
                to = "zh";
            }

            var appId = Plugin.Instance.Configuration.BaiduFanyiApiKey;
            var secretKey = Plugin.Instance.Configuration.BaiduFanyiSecretKey;

            var salt = _random.Next(100000).ToString(CultureInfo.InvariantCulture);
            var sign = EncryptString(appId + q + salt + secretKey);

            var param = new Dictionary<string, string>()
            {
                ["q"] = q,
                ["from"] = from,
                ["to"] = to,
                ["appid"] = appId,
                ["salt"] = salt,
                ["sign"] = sign,
            };

            using var content = new FormUrlEncodedContent(param);
            var resp = await _client.PostAsync(string.Empty, content).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            return JsonSerializer.Deserialize<BaiduFanyiResult>(json);
        }

        /// <summary>
        /// 计算MD5值
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static string EncryptString(string str)
        {
            using var md5 = SHA256.Create();
            // 将字符串转换成字节数组
            var byteOld = Encoding.UTF8.GetBytes(str);
            // 调用加密方法
            var byteNew = md5.ComputeHash(byteOld);
            return BitConverter.ToString(byteNew).Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase).ToLower(CultureInfo.CurrentCulture);
        }
    }
}
