using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JavScraper.Extensions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Tasks
{
    public class FixChineseSubtitleGenreTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;

        public FixChineseSubtitleGenreTask(
            ILoggerFactory loggerFactory,
            ILibraryManager libraryManager,
            IFileSystem fileSystem)
        {
            _logger = loggerFactory.CreateLogger<FixChineseSubtitleGenreTask>();
            _libraryManager = libraryManager;
            _fileSystem = fileSystem;
        }

        public string Name => Category + ": 修复缺失的中文字幕标签";

        public string Key => Category + "-FixChineseSubtitleGenre";

        public string Description => "修复缺失的中文字幕标签，需要在配置中勾选【给 -C 或 -C2 结尾的影片增加“中文字幕”标签】选项。";

        public string Category => "JavScraper";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Array.Empty<TaskTriggerInfo>();

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            await Task.Yield();
            if (Plugin.Instance.Configuration.AddChineseSubtitleGenre == false)
            {
                _logger.LogWarning("AddChineseSubtitleGenre option is not enabled.");
                return;
            }

            _logger.LogInformation("Running...");
            progress.Report(0);

            var collectionType = MediaBrowser.Model.Entities.CollectionTypeOptions.Movies;

            var libraryFolderPaths = _libraryManager.GetVirtualFolders()
                .Where(dir => dir.CollectionType == collectionType && dir.Locations?.Any() == true &&
                    dir.LibraryOptions.TypeOptions?.Any(o => o.MetadataFetchers?.Contains(Constants.PluginName) == true) == true)
                .SelectMany(o => o.Locations).ToList();

            var eligibleFiles = libraryFolderPaths.SelectMany(GetVideoFiles)
                .OrderBy(_fileSystem.GetCreationTimeUtc)
                .Where(fileInfo => _libraryManager.FindByPath(fileInfo.FullName, false) is Video)
                .ToList();

            _logger.LogInformation("{} files found", eligibleFiles.Count);
            if (eligibleFiles.Count == 0)
            {
                progress.Report(100);
                return;
            }

            var index = 0;
            foreach (var m in eligibleFiles)
            {
                try
                {
                    var result = await DoAsync(m).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "fileName = {}", m.FullName);
                }

                index++;
                progress.Report(index * 1.0 / eligibleFiles.Count * 100);
            }

            progress.Report(100);
        }

        private async Task<bool> DoAsync(FileSystemMetadata metadata)
        {
            if (_libraryManager.FindByPath(metadata.FullName, false) is not Movie movie)
            {
                _logger.LogError("the movie does not exists. {}", metadata.FullName);
                return false;
            }

            var hasChineseSubtitle = _fileSystem.HasChineseSubtitle(movie);

            if (!hasChineseSubtitle)
            {
                return false;
            }

            movie.AddGenre("中文字幕");
            await movie.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).ConfigureAwait(false);
            return true;
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
                return _fileSystem.GetFiles(path, true).Where(fileInfo => _libraryManager.FindByPath(fileInfo.FullName, false) is Video).ToList();
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogError(ex, "folder does not exist: {}", path);

                return new List<FileSystemMetadata>();
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Error getting files from {}", path);

                return new List<FileSystemMetadata>();
            }
        }
    }
}
