#if __JELLYFIN__

using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper
{
    public static class JellyfinExtensions
    {
        private static string[] SubtitleExtensions = new[] { ".srt", ".ssa", ".ass", ".sub", ".smi", ".sami", ".vtt", ".mpl" };

        public static bool IsSubtitleFile(this ILibraryManager _, string path)
        {
            var extension = Path.GetExtension(path);
            return ListHelper.ContainsIgnoreCase(SubtitleExtensions, extension);
        }

        public static void UpdateToRepository(this BaseItem item, ItemUpdateType type)
            => item.UpdateToRepository(type, default);

        /// <summary>
        /// 获取图片缓存路径
        /// </summary>
        /// <param name="appPaths"></param>
        /// <returns></returns>
        public static string GetImageCachePath(this IApplicationPaths appPaths)
            => appPaths.ImageCachePath;

        public static void WriteAllBytes(this IFileSystem fs, string path, byte[] bytes)
        {
            File.WriteAllBytes(path, bytes);
        }

        public static Task<byte[]> ReadAllBytesAsync(this IFileSystem fs, string path)
        {
            return Task.FromResult(File.ReadAllBytes(path));
        }

        public static bool DirectoryExists(this IFileSystem fileSystem, string path)
            => Directory.Exists(path);

        public static bool FileExists(this IFileSystem fileSystem, string path)
            => File.Exists(path);

        public static void CreateDirectory(this IFileSystem fileSystem, string path)
            => Directory.CreateDirectory(path);

        public static void CopyFile(this IFileSystem fileSystem, string source, string target, bool overwrite)
            => File.Copy(source, target, overwrite);

        public static void MoveFile(this IFileSystem fileSystem, string source, string target)
            => File.Move(source, target);

        public static void MoveDirectory(this IFileSystem fileSystem, string source, string target)
            => Directory.Move(source, target);

        public static void DeleteDirectory(this IFileSystem fileSystem, string path, bool recursive)
            => Directory.Delete(path, recursive);

        public static void Debug(this ILogger logger, string msg)
            => logger.LogDebug(msg);

        public static void Info(this ILogger logger, string msg)
            => logger.LogInformation(msg);

        public static void Warn(this ILogger logger, string msg)
            => logger.LogWarning(msg);

        public static void Error(this ILogger logger, string msg)
            => logger.LogError(msg);
    }
}

#endif