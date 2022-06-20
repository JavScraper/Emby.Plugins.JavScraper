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

        /// <summary>
        ///     Calculate the difference between 2 strings using the Levenshtein distance algorithm
        /// </summary>
        /// <param name="source1">First string</param>
        /// <param name="source2">Second string</param>
        /// <returns></returns>
        public static int CalculateLevenshteinDistance(this string source1, string source2, StringComparison comparisonType = StringComparison.OrdinalIgnoreCase) // O(n*m)
        {
            var source1Length = source1?.Length ?? 0;
            var source2Length = source2?.Length ?? 0;

            // First calculation, if one entry is empty return full length
            if (source1 == null || source1Length == 0)
            {
                return source2Length;
            }

            if (source2 == null || source2Length == 0)
            {
                return source1Length;
            }

#pragma warning disable CA1814 // 与多维数组相比，首选使用交错数组
            var matrix = new int[source1Length + 1, source2Length + 1];
#pragma warning restore CA1814 // 与多维数组相比，首选使用交错数组

            // Initialization of matrix with row size source1Length and columns size source2Length
            for (var i = 0; i <= source1Length; matrix[i, 0] = i++)
            {
            }

            for (var j = 0; j <= source2Length; matrix[0, j] = j++)
            {
            }

            // Calculate rows and collumns distances
            for (var i = 1; i <= source1Length; i++)
            {
                for (var j = 1; j <= source2Length; j++)
                {
                    var cost = source2[j - 1].ToString().Contains(source1[i - 1], comparisonType) ? 0 : 1;

                    matrix[i, j] = Math.Min(Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1), matrix[i - 1, j - 1] + cost);
                }
            }

            // return result
            return matrix[source1Length, source2Length];
        }

        public static string Join(this IEnumerable<string> values, string? separator = null) => string.Join(separator, values);

        public static bool TryMatch(this string input, Regex regex, out Match match)
        {
            match = regex.Match(input);
            return match.Success;
        }

        public static JsonElement? GetPropertyOrNull(this JsonElement element, string propertyName) => element.TryGetProperty(propertyName, out var property) ? property : null;
    }
}
