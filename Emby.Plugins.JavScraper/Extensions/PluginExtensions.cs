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
        public static JavVideoIndex GetJavVideoIndex(this IHasProviderIds result, IJsonSerializer _jsonSerializer)
        {
            if (result.ProviderIds.TryGetValue($"{Name}-Json", out string json) == false)
                return null;

            return _jsonSerializer.DeserializeFromString<JavVideoIndex>(json);
        }
    }
}