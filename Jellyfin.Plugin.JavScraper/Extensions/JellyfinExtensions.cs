using System;
using System.IO;
using System.Linq;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;

namespace Jellyfin.Plugin.JavScraper.Extensions
{
    public static class JellyfinExtensions
    {
        private static readonly string[] _subtitleExtensions = new[] { ".srt", ".ssa", ".ass", ".sub", ".smi", ".sami", ".vtt", ".mpl" };

        public static bool IsSubtitleFile(this string path)
        {
            var extension = Path.GetExtension(path);
            return _subtitleExtensions.Any(v => string.Equals(v, extension, System.StringComparison.OrdinalIgnoreCase));
        }

        public static bool HasChineseSubtitle(this IFileSystem fileSystem, Movie movie)
        {
            var hasChineseSubtitle = movie.Genres.Contains("中文字幕");
            hasChineseSubtitle = hasChineseSubtitle || new[] { "-C", "-C2", "_C", "_C2" }.Any(suffix => movie.Path.GetFileNameWithoutExtension().EndsWith(suffix, StringComparison.OrdinalIgnoreCase) || movie.Path.GetDirectoryName().EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            hasChineseSubtitle = hasChineseSubtitle || fileSystem.ExistsSubtitleFile(movie.Path);
            return hasChineseSubtitle;
        }

        /// <summary>
        /// 是否存在字幕文件
        /// </summary>
        /// <returns></returns>
        private static bool ExistsSubtitleFile(this IFileSystem fileSystem, string pathOfMovie) =>
            fileSystem.GetFilePaths(pathOfMovie.GetDirectoryName())
                .Any(v => v.StartsWith(pathOfMovie.GetFileNameWithoutExtension(), StringComparison.OrdinalIgnoreCase) && v.IsSubtitleFile());

        /// <summary>
        /// 获取图片缓存路径
        /// </summary>
        /// <param name="appPaths"></param>
        /// <returns></returns>
        public static string GetImageCachePath(this IApplicationPaths appPaths)
            => appPaths.ImageCachePath;
    }
}
