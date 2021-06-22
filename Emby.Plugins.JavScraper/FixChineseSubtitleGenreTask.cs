using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using System.Collections.Generic;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Common.Configuration;
using System.Linq;
using System.IO;
using MediaBrowser.Controller.Entities.Movies;
using Emby.Plugins.JavScraper.Configuration;

#if __JELLYFIN__
using Microsoft.Extensions.Logging;
#else
using MediaBrowser.Model.Logging;
#endif

namespace Emby.Plugins.JavScraper
{
    public class FixChineseSubtitleGenreTask : IScheduledTask
    {
        public string Name => Plugin.NAME + ": 修复缺失的中文字幕标签";
        public string Key => Plugin.NAME + "-FixChineseSubtitleGenre";
        public string Description => "修复缺失的中文字幕标签，需要在配置中勾选【给 -C 或 -C2 结尾的影片增加“中文字幕”标签】选项。";
        public string Category => "JavScraper";

        private readonly ILibraryManager _libraryManager;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IApplicationPaths appPaths;
        private readonly IProviderManager providerManager;
        private readonly ILibraryMonitor libraryMonitor;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;

        public FixChineseSubtitleGenreTask(
#if __JELLYFIN__
            ILoggerFactory logManager
#else
            ILogManager logManager
#endif
            , ILibraryManager libraryManager, IJsonSerializer _jsonSerializer, IApplicationPaths appPaths,
            IProviderManager providerManager,
            ILibraryMonitor libraryMonitor,
            IFileSystem fileSystem)
        {
            _logger = logManager.CreateLogger<FixChineseSubtitleGenreTask>();
            this._libraryManager = libraryManager;
            this._jsonSerializer = _jsonSerializer;
            this.appPaths = appPaths;
            this.providerManager = providerManager;
            this.libraryMonitor = libraryMonitor;
            this._fileSystem = fileSystem;
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
            => new TaskTriggerInfo[] { };

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            await Task.Yield();
            if (Plugin.Instance.Configuration.AddChineseSubtitleGenre == false)
            {
                _logger.Warn($"AddChineseSubtitleGenre option is not enabled.");
                return;
            }
            _logger.Info($"Running...");
            progress.Report(0);

            var libraryFolderPaths = _libraryManager.GetVirtualFolders()
                .Where(dir => dir.CollectionType == "movies" && dir.Locations?.Any() == true &&
                    dir.LibraryOptions.TypeOptions?.Any(o => o.MetadataFetchers?.Contains(Plugin.NAME) == true) == true)
                .SelectMany(o => o.Locations).ToList();

            var eligibleFiles = libraryFolderPaths.SelectMany(GetVideoFiles)
                .OrderBy(_fileSystem.GetCreationTimeUtc)
                .Where(i => IsVideoFile(i))
                .ToList();

            _logger.Info($"{eligibleFiles.Count} files found");
            if (eligibleFiles.Count == 0)
            {
                progress.Report(100);
                return;
            }

            int index = 0;
            foreach (var m in eligibleFiles)
            {
                try
                {
                    var r = Do(m);
                }
                catch (Exception ex)
                {
                    _logger.Error($"{m.FullName}  {ex.Message}");
                }
                index++;
                progress.Report(index * 1.0 / eligibleFiles.Count * 100);
            }
            progress.Report(100);
        }

        private bool Do(FileSystemMetadata m)
        {
            var movie = _libraryManager.FindByPath(m.FullName, false) as Movie;
            if (movie == null)
            {
                _logger.Error($"the movie does not exists. {m.FullName}");
                return false;
            }

            const string CHINESE_SUBTITLE_GENRE = "中文字幕";

            if (movie.Genres?.Contains(CHINESE_SUBTITLE_GENRE) == true)
                return true;

            var arr = new[] { Path.GetFileNameWithoutExtension(m.FullName), Path.GetFileName(Path.GetDirectoryName(m.FullName)) };
            var cc = new[] { "-C", "-C2", "_C", "_C2" };
            var has_chinese_subtitle = arr.Any(v => cc.Any(x => v.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
               || ExistsSubtitleFile(m);

            if (has_chinese_subtitle == false)
                return false;

            movie.AddGenre(CHINESE_SUBTITLE_GENRE);
            movie.UpdateToRepository(ItemUpdateType.MetadataEdit);
            return true;
        }

        /// <summary>
        /// 是否存在字幕文件
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <returns></returns>
        private bool ExistsSubtitleFile(FileSystemMetadata fileInfo)
        {
            try
            {
                var name = Path.GetFileNameWithoutExtension(fileInfo.FullName);
                var files = _fileSystem.GetFilePaths(Path.GetDirectoryName(fileInfo.FullName));
                return files.Any(v => v.StartsWith(name, StringComparison.OrdinalIgnoreCase) && _libraryManager.IsSubtitleFile(v
#if !__JELLYFIN__
                    .AsSpan()
#endif
                    ));
            }
            catch (Exception ex)
            {
                _logger.Error($"Error organizing file {fileInfo.Name}: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Gets the files to organize.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>IEnumerable{FileInfo}.</returns>
        private List<FileSystemMetadata> GetVideoFiles(string path)
        {
            try
            {
                return _fileSystem.GetFiles(path, true).Where(file => IsVideoFile(file)).ToList();
            }
            catch (DirectoryNotFoundException)
            {
                _logger.Info($"folder does not exist: {path}");

                return new List<FileSystemMetadata>();
            }
            catch (IOException ex)
            {
                _logger.Error($"Error getting files from {path}: {ex.Message}");

                return new List<FileSystemMetadata>();
            }
        }

        /// <summary>
        /// 是否是视频文件
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <returns></returns>
        private bool IsVideoFile(FileSystemMetadata fileInfo)
        {
            try
            {
                return _libraryManager.IsVideoFile(fileInfo.FullName
#if !__JELLYFIN__
                    .AsSpan()
#endif
                    );
            }
            catch (Exception ex)
            {
                _logger.Error($"Error organizing file {fileInfo.Name}: {ex.Message}");
            }

            return false;
        }
    }
}