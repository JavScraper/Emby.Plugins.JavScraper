using Emby.Plugins.JavScraper.Baidu;
using Emby.Plugins.JavScraper.Data;
#if __JELLYFIN__
using Microsoft.Extensions.Logging;
#else
using MediaBrowser.Model.Logging;
#endif
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper.Services
{
    /// <summary>
    /// 翻译服务
    /// </summary>
    public class TranslationService
    {
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ILogger _logger;
        private static NamedLockerAsync _locker = new NamedLockerAsync();

        /// <summary>
        /// 翻译
        /// </summary>
        /// <param name="jsonSerializer"></param>
        public TranslationService(IJsonSerializer jsonSerializer, ILogger logger)
        {
            _jsonSerializer = jsonSerializer;
            _logger = logger;
        }

        /// <summary>
        /// 翻译
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        public async Task<string> Fanyi(string src)
        {
            if (string.IsNullOrWhiteSpace(src))
                return src;

            var lang = Plugin.Instance.Configuration.BaiduFanyiLanguage?.Trim();
            if (string.IsNullOrWhiteSpace(lang))
                lang = "zh";

            var hash = Translation.CalcHash(src);

            using (await _locker.LockAsync(hash))
            {
                try
                {
                    var item = Plugin.Instance.db.Translations.FindOne(o => o.hash == hash && o.lang == lang);
                    if (item != null)
                        return item.dst;

                    var fanyi_result = await BaiduFanyiService.Fanyi(src, _jsonSerializer);
                    if (fanyi_result?.trans_result?.Any() == true)
                    {
                        var dst = string.Join("\n", fanyi_result.trans_result.Select(o => o.dst));
                        if (string.IsNullOrWhiteSpace(dst) == false)
                        {
                            item = new Translation()
                            {
                                hash = hash,
                                lang = lang,
                                src = src,
                                dst = dst,
                                created = DateTime.Now,
                                modified = DateTime.Now,
                            };
                            Plugin.Instance.db.Translations.Insert(item);
                            return dst;
                        }
                    }

                    return src;
                }
                catch (Exception ex)
                {
                    _logger.Error($"{src} {ex.Message}");
                }
            }

            return src;
        }

        /// <summary>
        /// 翻译
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        public async Task<List<string>> Fanyi(List<string> values)
        {
            if (values?.Any() != true)
                return values;

            var ls = new List<string>();

            foreach (var src in values)
                ls.Add(await Fanyi(src));

            return ls;
        }
    }
}