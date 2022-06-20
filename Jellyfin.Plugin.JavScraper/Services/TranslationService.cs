using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Extensions;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Services
{
    /// <summary>
    /// 翻译服务
    /// </summary>
    public class TranslationService
    {
        private readonly ILogger _logger;
        private readonly ApplicationDatabase _applicationDatabase;
        private static readonly NamedAsyncLocker _locker = new();

        /// <summary>
        /// 翻译
        /// </summary>
        public TranslationService(ILoggerFactory loggerFactory, ApplicationDatabase applicationDatabase)
        {
            _logger = loggerFactory.CreateLogger<TranslationService>();
            _applicationDatabase = applicationDatabase;
        }

        /// <summary>
        /// 翻译
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        public async Task<string> Translate(string src)
        {
            if (string.IsNullOrWhiteSpace(src))
            {
                return src;
            }

            var lang = Plugin.Instance.Configuration.BaiduFanyiLanguage.Trim();
            if (string.IsNullOrWhiteSpace(lang))
            {
                lang = "zh";
            }

            var hash = CalcHash(src);

            using (await _locker.WaitAsync(hash).ConfigureAwait(false))
            {
                try
                {
                    var item = _applicationDatabase.Translations.FindOne(o => o.Hash == hash && o.Lang == lang);
                    if (item != null)
                    {
                        return item.Dst;
                    }

                    var fanyi_result = await BaiduTranslationService.Translate(src).ConfigureAwait(false);
                    if (fanyi_result?.TransResult?.Any() == true)
                    {
                        var dst = string.Join("\n", fanyi_result.TransResult.Select(o => o.Dst));
                        if (string.IsNullOrWhiteSpace(dst) == false)
                        {
                            item = new Translation()
                            {
                                Hash = hash,
                                Lang = lang,
                                Src = src,
                                Dst = dst,
                                Created = DateTime.Now,
                                Modified = DateTime.Now,
                            };
                            _applicationDatabase.Translations.Insert(item);
                            return dst;
                        }
                    }

                    return src;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "fail to translate for src={}", src);
                }
            }

            return src;
        }

        /// <summary>
        /// 翻译
        /// </summary>
        /// <returns></returns>
        public async Task<IReadOnlyList<string>> Translate(IReadOnlyList<string> values)
        {
            if (!values.Any())
            {
                return values;
            }

            var ls = new List<string>();

            foreach (var src in values)
            {
                ls.Add(await Translate(src).ConfigureAwait(false));
            }

            return ls;
        }

        /// <summary>
        /// 计算原始文本的 Hash
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        public static string CalcHash(string src)
        {
            if (string.IsNullOrWhiteSpace(src))
            {
                return string.Empty;
            }

            using var md5 = SHA256.Create();
            var result = md5.ComputeHash(Encoding.UTF8.GetBytes(src));
            var strResult = BitConverter.ToString(result);
            return strResult.Replace("-", string.Empty, StringComparison.Ordinal).ToLower(System.Globalization.CultureInfo.CurrentCulture);
        }
    }
}
