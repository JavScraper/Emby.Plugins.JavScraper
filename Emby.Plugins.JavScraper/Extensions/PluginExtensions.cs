using Emby.Plugins.JavScraper.Scrapers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Serialization;

namespace Emby.Plugins.JavScraper
{
    /// <summary>
    /// 扩展
    /// </summary>
    public static class PluginExtensions
    {
        public static string Name => Plugin.NAME;

        public static string PersonName => Plugin.NAME + "-Actress";

        /// <summary>
        /// 设置视频信息
        /// </summary>
        /// <param name="result"></param>
        /// <param name="_jsonSerializer"></param>
        /// <param name="m"></param>
        /// <returns></returns>
        public static IHasProviderIds SetJavVideoIndex(this IHasProviderIds result, IJsonSerializer _jsonSerializer, JavVideoIndex m)
        {
            result.ProviderIds[Name] = m.Num;
            result.ProviderIds[$"{Name}-Json"] = _jsonSerializer.SerializeToString(m);
            result.ProviderIds[$"{Name}-Url"] = m.Url;

            return result;
        }

        /// <summary>
        /// 获取视频信息
        /// </summary>
        /// <param name="result"></param>
        /// <param name="_jsonSerializer"></param>
        /// <returns></returns>
        public static JavVideo GetJavVideoIndex(this IHasProviderIds result, IJsonSerializer _jsonSerializer)
        {
            if (result.ProviderIds.TryGetValue($"{Name}-Json", out string json) == false)
                return null;

            return _jsonSerializer.DeserializeFromString<JavVideo>(json);
        }

        /// <summary>
        /// 设置头像信息
        /// </summary>
        /// <param name="result"></param>
        /// <param name="_jsonSerializer"></param>
        /// <param name="m"></param>
        /// <returns></returns>
        public static IHasProviderIds SetJavPersonIndex(this IHasProviderIds result, IJsonSerializer _jsonSerializer, JavPersonIndex m)
        {
            result.ProviderIds[PersonName] = m.Url;
            result.ProviderIds[$"{PersonName}-Json"] = _jsonSerializer.SerializeToString(m);
            result.ProviderIds[$"{PersonName}-Url"] = m.Url;

            return result;
        }

        /// <summary>
        /// 获取头像信息
        /// </summary>
        /// <param name="result"></param>
        /// <param name="_jsonSerializer"></param>
        /// <returns></returns>
        public static JavPersonIndex GetJavPersonIndex(this IHasProviderIds result, IJsonSerializer _jsonSerializer)
        {
            if (result.ProviderIds.TryGetValue($"{PersonName}-Json", out string json) == false)
                return null;

            return _jsonSerializer.DeserializeFromString<JavPersonIndex>(json);
        }
    }
}