using System.Text.Json;
using Jellyfin.Plugin.JavScraper.Scrapers.Model;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JavScraper.Extensions
{
    public static class PluginExtensions
    {
        public static string Name => "JavScraper";

        public static string PersonName => "JavScraper-Actress";

        public static IHasProviderIds SetJavVideoIndex(this IHasProviderIds result, JavVideoIndex vedioIndex)
        {
            result.ProviderIds[Name] = vedioIndex.Num;
            result.ProviderIds[$"{Name}-Url"] = vedioIndex.Url;
            result.ProviderIds[$"{Name}-Json"] = vedioIndex.ToJson();

            return result;
        }

        public static JavVideo? GetJavVideo(this IHasProviderIds result) => result.ProviderIds.TryGetValue($"{Name}-Json", out var json) ? JsonSerializer.Deserialize<JavVideo>(json) : null;

        public static JavVideoIndex? GetJavVideoIndex(this IHasProviderIds result) => result.ProviderIds.TryGetValue($"{Name}-Json", out var json) ? JsonSerializer.Deserialize<JavVideoIndex>(json) : null;

        public static IHasProviderIds SetJavPersonIndex(this IHasProviderIds result, JavPersonIndex index)
        {
            result.ProviderIds[PersonName] = index.Url;
            result.ProviderIds[$"{PersonName}-Json"] = index.ToJson();
            result.ProviderIds[$"{PersonName}-Url"] = index.Url;

            return result;
        }

        public static JavPersonIndex? GetJavPersonIndex(this IHasProviderIds result) => result.ProviderIds.TryGetValue($"{PersonName}-Json", out var json) ? JsonSerializer.Deserialize<JavPersonIndex>(json) : null;

        public static JavPerson? GetJavPerson(this IHasProviderIds result) => result.ProviderIds.TryGetValue($"{PersonName}-Json", out var json) ? JsonSerializer.Deserialize<JavPerson>(json) : null;
    }
}
