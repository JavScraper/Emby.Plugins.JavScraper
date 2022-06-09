﻿using System;

namespace Jellyfin.Plugin.JavScraper.Extensions
{
    /// <summary>
    /// 相似度计算
    /// </summary>
    public static class LevenshteinDistance
    {
        /// <summary>
        ///     Calculate the difference between 2 strings using the Levenshtein distance algorithm
        /// </summary>
        /// <param name="source1">First string</param>
        /// <param name="source2">Second string</param>
        /// <returns></returns>
        public static int Calculate(string source1, string source2, StringComparison comparisonType = StringComparison.OrdinalIgnoreCase) // O(n*m)
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
    }
}
