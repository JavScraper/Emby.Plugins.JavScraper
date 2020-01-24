using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace Emby.Plugins.JavScraper
{
    public static class StringExtensions
    {
        private static readonly Regex WebUrlExpression = new Regex(@"(http|https)://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?", RegexOptions.Singleline | RegexOptions.Compiled);

        [DebuggerStepThrough]
        public static bool IsWebUrl(this string target)
        {
            return !string.IsNullOrEmpty(target) && WebUrlExpression.IsMatch(target);
        }

        public static string TrimStart(this string target, string trimString)
        {
            if (string.IsNullOrEmpty(trimString))
                return target;

            string result = target;
            while (result.StartsWith(trimString, StringComparison.OrdinalIgnoreCase))
            {
                result = result.Substring(trimString.Length);
            }

            return result;
        }

        public static string TrimEnd(this string target, params string[] trimStrings)
        {
            trimStrings = trimStrings?.Where(o => string.IsNullOrEmpty(o) == false).Distinct().ToArray();
            if (trimStrings?.Any() != true)
                return target;

            var found = false;

            do
            {
                found = false;
                foreach (var trimString in trimStrings)
                {
                    while (target.EndsWith(trimString, StringComparison.OrdinalIgnoreCase))
                    {
                        target = target.Substring(0, target.Length - trimString.Length);
                        found = true;
                    }
                }
            } while (found);
            return target;
        }

        public static string Trim(this string target, string trimString)
            => target.TrimStart(trimString).TrimEnd(trimString);
    }
}