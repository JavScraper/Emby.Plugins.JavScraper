using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.JavScraper
{
    public static class Constants
    {
        public const string PluginName = "JavScrapers";

        public static class HttpClient
        {
            public const bool LogRequest = false;
            public const bool LogResponse = false;
        }

        public static class RegexExpression
        {
            public static readonly Regex Number = new(@"\d+", RegexOptions.Compiled);
            public static readonly Regex Float = new(@"[0-9]+(\.[0-9]+)?", RegexOptions.Compiled);
        }
    }
}
