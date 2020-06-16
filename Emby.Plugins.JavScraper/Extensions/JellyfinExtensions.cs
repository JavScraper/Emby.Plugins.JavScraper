#if __JELLYFIN__

using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper
{
    public static class JellyfinExtensions
    {
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

        public static void Info(this ILogger logger, string msg)
            => logger.LogInformation(msg);

        public static void Warn(this ILogger logger, string msg)
            => logger.LogWarning(msg);

        public static void Error(this ILogger logger, string msg)
            => logger.LogError(msg);
    }
}

#endif