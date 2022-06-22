using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JavScraper.Configuration;
using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Extensions;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Tasks
{
    public class JavOrganizeTask : IScheduledTask
    {
        private readonly ApplicationDatabase _applicationDatabase;
        private readonly ILibraryManager _libraryManager;
        private readonly IApplicationPaths _appPaths;
        private readonly IProviderManager _providerManager;
        private readonly ILibraryMonitor _libraryMonitor;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;

        public JavOrganizeTask(
            ApplicationDatabase applicationDatabase,
            ILoggerFactory logManager,
            ILibraryManager libraryManager,
            IApplicationPaths appPaths,
            IProviderManager providerManager,
            ILibraryMonitor libraryMonitor,
            IFileSystem fileSystem)
        {
            _logger = logManager.CreateLogger<JavOrganizeTask>();
            _libraryManager = libraryManager;
            _appPaths = appPaths;
            _providerManager = providerManager;
            _libraryMonitor = libraryMonitor;
            _fileSystem = fileSystem;
            _applicationDatabase = applicationDatabase;
        }

        public string Name => "JavOrganize: 立即整理日本电影文件<span style='color:#FF0000;'>【实验功能】</span>";

        public string Key => "JavOrganize";

        public string Description => "立即整理日本电影文件，使用之前请先<a data-navmenuid='/configurationpage?name=JavOrganize' is='Jellyfin-linkbutton' class='button-link Jellyfin-button' href='configurationpage?name=JavOrganize' title='配置'>配置</a>规则。<br /><span style='color:#FF0000;'>该功能目前尚处于实验阶段，请谨慎使用及做好数据备份。</span><span style='color:#FF0000;'>由此插件引起的数据丢失或其他任何问题，作者不负任何责任。</span>";

        public string Category => Constants.PluginName;

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
            => Array.Empty<TaskTriggerInfo>();

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Running...");
            progress.Report(0);

            var options = Plugin.Instance.Configuration.JavOrganizationOptions;
            var empty = Plugin.Instance.Configuration.TitleFormatEmptyValue;

            if (!options.WatchLocations.Any())
            {
                _logger.LogWarning("source folder cannot be empty.");
                return;
            }

            if (string.IsNullOrWhiteSpace(options.TargetLocation))
            {
                _logger.LogWarning("target folder is empty.");
                return;
            }

            if (string.IsNullOrWhiteSpace(options.MovieFolderPattern) && string.IsNullOrWhiteSpace(options.MoviePattern))
            {
                _logger.LogWarning("folder pattern and file name pattern cannot be empty at the same time.");
                return;
            }

            var libraryFolderPaths = _libraryManager.GetVirtualFolders()
                .Where(dir => dir.CollectionType == CollectionTypeOptions.Movies && dir.Locations.Any() && dir.LibraryOptions.TypeOptions.Any(o => o.MetadataFetchers.Contains("JavScraper")))
                .SelectMany(o => o.Locations).ToList();

            var watchLocations = options.WatchLocations
                .Where(o => IsValidWatchLocation(o, libraryFolderPaths))
                .ToList();

            var eligibleFiles = watchLocations.SelectMany(GetFilesToOrganize)
                .OrderBy(_fileSystem.GetCreationTimeUtc)
                .Where(metadata => EnableOrganization(metadata, options))
                .ToList();

            var processedFolders = new HashSet<string>();

            _logger.LogInformation("{} files found", eligibleFiles.Count);
            if (eligibleFiles.Count == 0)
            {
                progress.Report(100);
                return;
            }

            var index = 0;
            foreach (var metadata in eligibleFiles)
            {
                try
                {
                    var isSuccess = await DoAsync(options, empty, metadata).ConfigureAwait(false);
                    if (isSuccess && !processedFolders.Contains(metadata.FullName, StringComparer.OrdinalIgnoreCase))
                    {
                        processedFolders.Add(metadata.FullName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{}", metadata.FullName);
                }

                index++;
                progress.Report(index * 1.0 / eligibleFiles.Count * 100);
            }

            progress.Report(99);

            var deleteExtensions = options.LeftOverFileExtensionsToDelete
                .Select(e => e.Trim().TrimStart('.'))
                .Where(e => !string.IsNullOrEmpty(e))
                .Select(e => "." + index)
                .ToList();

            Clean(processedFolders, watchLocations, options.DeleteEmptyFolders, deleteExtensions);

            // Extended Clean
            if (options.ExtendedClean)
            {
                Clean(watchLocations, watchLocations, options.DeleteEmptyFolders, deleteExtensions);
            }

            progress.Report(100);
        }

        private async Task<bool> DoAsync(JavOrganizationOptions options, string empty, FileSystemMetadata metadata)
        {
            if (_libraryManager.FindByPath(metadata.FullName, false) is not Movie movie)
            {
                _logger.LogError("the movie does not exists. {}", metadata.FullName);
                return false;
            }

            var vedio = movie.GetJavVideo();
            if (vedio == null)
            {
                _logger.LogError("jav video index does not exists. {}", metadata.FullName);
                return false;
            }

            // 尝试还原 JavVideoIndex

            if (!vedio.Genres.Any() || !vedio.Actors.Any())
            {
                var result = _applicationDatabase.FindJavVideo(vedio.Provider, vedio.Url);
                if (result != null)
                {
                    vedio = result;
                }
            }

            if (!vedio.Genres.Any() && movie.Genres.Any())
            {
                vedio.Genres = movie.Genres.ToList();
            }

            if (!vedio.Actors.Any() || string.IsNullOrWhiteSpace(vedio.Director))
            {
                var persons = _libraryManager.GetPeople(movie);
                if (persons.Any())
                {
                    if (!vedio.Actors.Any())
                    {
                        vedio.Actors = persons.Where(person => person.Type == PersonType.Actor)
                            .Select(person => person.Name)
                            .ToList();
                    }

                    if (string.IsNullOrWhiteSpace(vedio.Director))
                    {
                        vedio.Director = persons.Where(person => person.Type == PersonType.Director)
                            .Select(person => person.Name)
                            .FirstOrDefault(string.Empty);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(vedio.Studio) && movie.Studios.Any())
            {
                vedio.Studio = movie.Studios.FirstOrDefault(string.Empty);
            }

            if (string.IsNullOrWhiteSpace(vedio.Overview) && !string.IsNullOrWhiteSpace(movie.Overview))
            {
                vedio.Overview = movie.Overview;
            }

            if (string.IsNullOrWhiteSpace(vedio.OriginalTitle) && !string.IsNullOrWhiteSpace(movie.OriginalTitle))
            {
                vedio.OriginalTitle = movie.OriginalTitle;
            }

            if (string.IsNullOrWhiteSpace(vedio.Set) && !string.IsNullOrWhiteSpace(movie.CollectionName))
            {
                vedio.Set = movie.CollectionName;
            }

            if (string.IsNullOrWhiteSpace(vedio.Date) && movie.PremiereDate != null)
            {
                vedio.Date = movie.PremiereDate.Value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            if (string.IsNullOrWhiteSpace(vedio.Date) && movie.ProductionYear > 0)
            {
                vedio.Date = $"{movie.ProductionYear}-01-01";
            }

            // 1，文件名中可能包含路径，
            // 2，去除路径中非法字符
            // 3，路径分隔符
            // 4，文件夹或者文件名中包含-C/-C2 中文字幕
            // 5，移动以文件名开通的文件
            // 6，移动某些特定文件名的文件
            // 7，替换nfo文件内的路径
            // 8，复制nfo中的其他文件?

            var hasChineseSubtitle = _fileSystem.HasChineseSubtitle(movie);

            var targetDir = options.TargetLocation;
            if (!string.IsNullOrWhiteSpace(options.MovieFolderPattern))
            {
                targetDir = Path.Combine(targetDir, vedio.GetFormatName(options.MovieFolderPattern, empty, true));
            }

            string targetFilenameWithoutExt;
            if (!string.IsNullOrWhiteSpace(options.MoviePattern))
            {
                // 文件名部分
                targetFilenameWithoutExt = vedio.GetFormatName(options.MoviePattern, empty, true);
            }
            else
            {
                targetFilenameWithoutExt = targetDir.GetFileName();
                targetDir = targetDir.GetDirectoryName();
            }

            // 文件名（含扩展名）
            var targetFilename = targetFilenameWithoutExt + Path.GetExtension(metadata.FullName);
            // 目标全路径
            var targetMovieFile = Path.GetFullPath(Path.Combine(targetDir, targetFilename));

            // 文件名中可能包含路基，所以需要重新计算文件名
            targetFilename = targetMovieFile.GetFileName();
            targetFilenameWithoutExt = targetFilename.GetFileNameWithoutExtension();
            targetDir = targetMovieFile.GetDirectoryName();

            if (hasChineseSubtitle && options.AddChineseSubtitleSuffix is >= 1 and <= 3) // 中文字幕
            {
                if (options.AddChineseSubtitleSuffix == 1 || options.AddChineseSubtitleSuffix == 3)
                {
                    // 包含在文件夹中
                    targetDir += "-C";
                }

                if (options.AddChineseSubtitleSuffix == 2 || options.AddChineseSubtitleSuffix == 3)
                {
                    // 包含在文件名中
                    targetFilenameWithoutExt += "-C";
                }

                targetFilename = targetFilenameWithoutExt + Path.GetExtension(targetFilename);
                targetMovieFile = Path.GetFullPath(Path.Combine(targetDir, targetFilename));
            }

            if (!_fileSystem.DirectoryExists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // 老的文件名
            var sourceName = metadata.FullName.GetFileNameWithoutExtension();
            var sourceDir = metadata.FullName.GetDirectoryName();

            // 已经存在的就跳过
            if (!options.OverwriteExistingFiles && _fileSystem.FileExists(targetMovieFile))
            {
                _logger.LogError("target directory contains the file with the same name: {}", targetMovieFile);
                return false;
            }

            var sourceFiles = _fileSystem.GetFiles(sourceDir);
            var pendingFiles = new List<(string From, string To)>();
            foreach (var path in sourceFiles.Select(o => o.FullName))
            {
                var fileName = Path.GetFileName(path);
                if (fileName.StartsWith(sourceName, StringComparison.OrdinalIgnoreCase))
                {
                    fileName = string.Concat(targetFilenameWithoutExt, fileName.AsSpan(sourceName.Length));
                    pendingFiles.Add((path, Path.Combine(targetDir, fileName)));
                }
                else if (fileName.StartsWith("fanart", StringComparison.OrdinalIgnoreCase) || fileName.StartsWith("poster", StringComparison.OrdinalIgnoreCase) || fileName.StartsWith("clearart", StringComparison.OrdinalIgnoreCase))
                {
                    pendingFiles.Add((path, Path.Combine(targetDir, fileName)));
                }
            }

            foreach (var (from, to) in pendingFiles)
            {
                if (options.OverwriteExistingFiles == false && _fileSystem.FileExists(to))
                {
                    _logger.LogInformation("FileSkip: from:{} to:{}", from, to);
                    return false;
                }

                if (options.CopyOriginalFile)
                {
                    File.Copy(from, to, options.OverwriteExistingFiles);
                    _logger.LogInformation("FileCopy: from:{} to:{}", from, to);
                }
                else
                {
                    File.Move(from, to);
                    _logger.LogInformation("FileMove: from:{} to:{}", from, to);
                }
            }

            try
            {
                // 非必须的
                var sourceFilePath = Path.Combine(sourceDir, "extrafanart");
                var targetFilePath = Path.Combine(targetDir, "extrafanart");
                if (Directory.Exists(sourceFilePath) && (options.OverwriteExistingFiles || !Directory.Exists(targetFilePath)))
                {
                    if (options.CopyOriginalFile)
                    {
                        if (!_fileSystem.DirectoryExists(targetFilePath))
                        {
                            Directory.CreateDirectory(targetFilePath);
                        }

                        foreach (var f in _fileSystem.GetFiles(sourceFilePath))
                        {
                            File.Copy(f.FullName, Path.Combine(targetFilePath, f.Name), options.OverwriteExistingFiles);
                        }

                        _logger.LogInformation("DirectoryCopy. source:{} target:{}", sourceFilePath, targetFilePath);
                    }
                    else
                    {
                        Directory.Move(sourceFilePath, targetFilePath);
                        _logger.LogInformation("DirectoryMove. source:{} target:{}", sourceFilePath, targetFilePath);
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }

            // 更新 nfo 文件
            foreach (var nfo in pendingFiles.Where(o => o.To.EndsWith(".nfo", StringComparison.OrdinalIgnoreCase)).Select(o => o.To))
            {
                var txt = await File.ReadAllTextAsync(nfo).ConfigureAwait(false);
                if (!txt.Contains(sourceDir, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                txt = txt.Replace(sourceDir, targetDir, StringComparison.OrdinalIgnoreCase);
                await File.WriteAllTextAsync(nfo, txt).ConfigureAwait(false);
            }

            movie.Path = targetMovieFile;
            await movie.UpdateToRepositoryAsync(ItemUpdateType.MetadataImport, CancellationToken.None).ConfigureAwait(false);
            return true;
        }

        private bool EnableOrganization(FileSystemMetadata fileInfo, JavOrganizationOptions options)
        {
            var minFileBytes = options.MinFileSizeMb * 1024 * 1024;

            try
            {
                return _libraryManager.FindByPath(fileInfo.FullName, false) is Movie && fileInfo.Length >= minFileBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error organizing file {}", fileInfo.Name);
            }

            return false;
        }

        private void Clean(IEnumerable<string> paths, List<string> watchLocations, bool deleteEmptyFolders, List<string> deleteExtensions)
        {
            foreach (var path in paths)
            {
                if (deleteExtensions.Count > 0)
                {
                    DeleteLeftOverFiles(path, deleteExtensions);
                }

                if (deleteEmptyFolders)
                {
                    DeleteEmptyFolders(path, watchLocations);
                }
            }
        }

        /// <summary>
        /// Deletes the left over files.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="extensions">The extensions.</param>
        private void DeleteLeftOverFiles(string path, IEnumerable<string> extensions)
        {
            var eligibleFiles = _fileSystem.GetFilePaths(path, extensions.ToArray(), false, true).ToList();

            foreach (var file in eligibleFiles)
            {
                try
                {
                    _fileSystem.DeleteFile(file);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting file {File}", file);
                }
            }
        }

        /// <summary>
        /// Deletes the empty folders.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="watchLocations">The path.</param>
        private void DeleteEmptyFolders(string path, List<string> watchLocations)
        {
            try
            {
                foreach (var d in _fileSystem.GetDirectoryPaths(path))
                {
                    DeleteEmptyFolders(d, watchLocations);
                }

                var entries = _fileSystem.GetFileSystemEntryPaths(path);

                if (!entries.Any() && !watchLocations.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        _logger.LogDebug("Deleting empty directory {}", path);
                        Directory.Delete(path, false);
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                    catch (IOException)
                    {
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private bool IsValidWatchLocation(string watchLocation, IEnumerable<string> libraryFolderPaths)
        {
            if (libraryFolderPaths.Any(path => string.Equals(path, watchLocation, StringComparison.Ordinal) || _fileSystem.ContainsSubPath(path, watchLocation)))
            {
                return true;
            }

            _logger.LogInformation("Folder {Path} is not eligible for jav-organize because it is not part of an Jellyfin library", watchLocation);
            return false;
        }

        /// <summary>
        /// Gets the files to organize.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>IEnumerable{FileInfo}.</returns>
        private List<FileSystemMetadata> GetFilesToOrganize(string path)
        {
            try
            {
                return _fileSystem.GetFiles(path, true).ToList();
            }
            catch (DirectoryNotFoundException)
            {
                _logger.LogInformation("Auto-Organize watch folder does not exist: {Path}", path);

                return new List<FileSystemMetadata>(0);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Error getting files from {Path}", path);

                return new List<FileSystemMetadata>(0);
            }
        }
    }
}
