using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace Jellyfin.Plugin.JavScraper.Extensions
{
    public static class StringExtensions
    {
        private static readonly Regex _webUrlExpression = new(@"(http|https)://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?", RegexOptions.Singleline | RegexOptions.Compiled);

        public static bool IsWebUrl([NotNullWhen(true)] this string? target) => !string.IsNullOrWhiteSpace(target) && _webUrlExpression.IsMatch(target);

        public static string GetFileName(this string path) => Path.GetFileName(path) ?? throw new ArgumentException("can not parse the path", nameof(path));

        public static string GetDirectoryName(this string path) => Path.GetDirectoryName(path) ?? throw new ArgumentException("can not parse the path", nameof(path));

        public static string GetFileNameWithoutExtension(this string path) => Path.GetFileNameWithoutExtension(path) ?? throw new ArgumentException("can not parse the path", nameof(path));

        public static string TrimStart(this string target, string trimString)
        {
            if (string.IsNullOrEmpty(trimString))
            {
                return target;
            }

            var result = target;
            while (result.StartsWith(trimString, StringComparison.OrdinalIgnoreCase))
            {
                result = result[trimString.Length..];
            }

            return result;
        }

        public static string TrimEnd(this string target, params string[] trimStrings)
        {
            trimStrings = trimStrings.Where(o => string.IsNullOrEmpty(o) == false).Distinct().ToArray();
            if (!trimStrings.Any())
            {
                return target;
            }

            var found = false;

            do
            {
                found = false;
                foreach (var trimString in trimStrings)
                {
                    while (target.EndsWith(trimString, StringComparison.OrdinalIgnoreCase))
                    {
                        target = target[..^trimString.Length];
                        found = true;
                    }
                }
            }
            while (found);
            return target;
        }

        public static bool Contains(this IEnumerable<string> enumerable, string o, StringComparison stringComparison) => enumerable.Any(element => element.Equals(o, stringComparison));

        public static string ToJson(this object any) => JsonSerializer.Serialize(any, new JsonSerializerOptions { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) });

        public static T? FromJson<T>(this string json) => JsonSerializer.Deserialize<T>(json);
    }
}
