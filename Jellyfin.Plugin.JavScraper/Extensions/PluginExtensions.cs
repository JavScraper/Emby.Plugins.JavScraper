using System.Text.Json;
using Jellyfin.Plugin.JavScraper.Scrapers.Model;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JavScraper.Extensions
{
    /// <summary>
    /// 扩展
    /// </summary>
    public static class PluginExtensions
    {
        public static string Name => "JavScraper";

        public static string PersonName => "JavScraper - Actress";

        /// <summary>
        /// 设置视频信息
        /// </summary>
        /// <param name="result"></param>
        /// <param name="vedioIndex"></param>
        /// <returns></returns>
        public static IHasProviderIds SetJavVideoIndex(this IHasProviderIds result, JavVideoIndex vedioIndex)
        {
            result.ProviderIds[Name] = vedioIndex.Num;
            result.ProviderIds[$"{Name}-Url"] = vedioIndex.Url;
            result.ProviderIds[$"{Name}-Json"] = JsonSerializer.Serialize(vedioIndex);

            return result;
        }

        /// <summary>
        /// 获取视频信息
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public static JavVideo? GetJavVideoIndex(this IHasProviderIds result)
        {
            if (result.ProviderIds.TryGetValue($"{Name}-Json", out var json))
            {
                return JsonSerializer.Deserialize<JavVideo>(json);
            }

            return null;
        }

        /// <summary>
        /// 设置头像信息
        /// </summary>
        /// <param name="result"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static IHasProviderIds SetJavPersonIndex(this IHasProviderIds result, JavPersonIndex index)
        {
            result.ProviderIds[PersonName] = index.Url;
            result.ProviderIds[$"{PersonName}-Json"] = JsonSerializer.Serialize(index);
            result.ProviderIds[$"{PersonName}-Url"] = index.Url;

            return result;
        }

        /// <summary>
        /// 获取头像信息
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public static JavPersonIndex? GetJavPersonIndex(this IHasProviderIds result)
        {
            if (result.ProviderIds.TryGetValue($"{PersonName}-Json", out var json) == false)
            {
                return null;
            }

            return JsonSerializer.Deserialize<JavPersonIndex>(json);
        }
    }
}
