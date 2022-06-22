using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.JavScraper
{
    public static class Constants
    {
        public const string PluginName = "JavScraper";
        public const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.130 Safari/537.36";

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
