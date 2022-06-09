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
        private readonly ApplicationDbContext _applicationDbContext;
        private readonly ILibraryManager _libraryManager;
        private readonly IApplicationPaths _appPaths;
        private readonly IProviderManager _providerManager;
        private readonly ILibraryMonitor _libraryMonitor;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;

        public JavOrganizeTask(
            ApplicationDbContext applicationDbContext,
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
            _applicationDbContext = applicationDbContext;
        }

        public string Name { get; } = "JavOrganize: 立即整理日本电影文件<span style='color:#FF0000;'>【实验功能】</span>";

        public string Key { get; } = "JavOrganize";

        public string Description { get; } = "立即整理日本电影文件，使用之前请先<a data-navmenuid='/configurationpage?name=JavOrganize' is='Jellyfin-linkbutton' class='button-link Jellyfin-button' href='configurationpage?name=JavOrganize' title='配置'>配置</a>规则。<br /><span style='color:#FF0000;'>该功能目前尚处于实验阶段，请谨慎使用及做好数据备份。</span><span style='color:#FF0000;'>由此插件引起的数据丢失或其他任何问题，作者不负任何责任。</span>";

        public string Category => "JavScraper";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
            => Array.Empty<TaskTriggerInfo>();

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Running...");
            progress.Report(0);

            var options = Plugin.Instance.Configuration.JavOrganizationOptions;
            var empty = Plugin.Instance.Configuration.TitleFormatEmptyValue;

            if (options == null || !options.WatchLocations.Any())
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
                    var r = await DoAsync(options, empty, metadata).ConfigureAwait(false);
                    if (r && !processedFolders.Contains(metadata.FullName, StringComparer.OrdinalIgnoreCase))
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

            var vedioIndex = movie.GetJavVideoIndex();
            if (vedioIndex == null)
            {
                _logger.LogError("jav video index does not exists. {}", metadata.FullName);
                return false;
            }

            // 尝试还原 JavVideoIndex

            if (!vedioIndex.Genres.Any() || !vedioIndex.Actors.Any())
            {
                var vedio = _applicationDbContext.FindJavVideo(vedioIndex.Provider, vedioIndex.Url);
                if (vedio != null)
                {
                    vedioIndex = vedio;
                }
            }

            if (!vedioIndex.Genres.Any() && movie.Genres.Any())
            {
                vedioIndex.Genres = movie.Genres.ToList();
            }

            if (!vedioIndex.Actors.Any() || string.IsNullOrWhiteSpace(vedioIndex.Director))
            {
                var persons = _libraryManager.GetPeople(movie);
                if (persons.Any())
                {
                    if (!vedioIndex.Actors.Any())
                    {
                        vedioIndex.Actors = persons.Where(person => person.Type == PersonType.Actor)
                            .Select(person => person.Name)
                            .ToList();
                    }

                    if (string.IsNullOrWhiteSpace(vedioIndex.Director))
                    {
                        vedioIndex.Director = persons.Where(person => person.Type == PersonType.Director)
                            .Select(person => person.Name)
                            .FirstOrDefault(string.Empty);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(vedioIndex.Studio) && movie.Studios.Any())
            {
                vedioIndex.Studio = movie.Studios.FirstOrDefault(string.Empty);
            }

            if (string.IsNullOrWhiteSpace(vedioIndex.Overview) && !string.IsNullOrWhiteSpace(movie.Overview))
            {
                vedioIndex.Overview = movie.Overview;
            }

            if (string.IsNullOrWhiteSpace(vedioIndex.OriginalTitle) && !string.IsNullOrWhiteSpace(movie.OriginalTitle))
            {
                vedioIndex.OriginalTitle = movie.OriginalTitle;
            }

            if (string.IsNullOrWhiteSpace(vedioIndex.Set) && !string.IsNullOrWhiteSpace(movie.CollectionName))
            {
                vedioIndex.Set = movie.CollectionName;
            }

            if (string.IsNullOrWhiteSpace(vedioIndex.Date) && movie.PremiereDate != null)
            {
                vedioIndex.Date = movie.PremiereDate.Value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            if (string.IsNullOrWhiteSpace(vedioIndex.Date) && movie.ProductionYear > 0)
            {
                vedioIndex.Date = $"{movie.ProductionYear}-01-01";
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

            var target_dir = options.TargetLocation;
            if (!string.IsNullOrWhiteSpace(options.MovieFolderPattern))
            {
                target_dir = Path.Combine(target_dir, vedioIndex.GetFormatName(options.MovieFolderPattern, empty, true));
            }

            string target_filename_without_ext;
            if (!string.IsNullOrWhiteSpace(options.MoviePattern))
            {
                // 文件名部分
                target_filename_without_ext = vedioIndex.GetFormatName(options.MoviePattern, empty, true);
            }
            else
            {
                target_filename_without_ext = target_dir.GetFileName();
                target_dir = target_dir.GetDirectoryName();
            }

            // 文件名（含扩展名）
            var target_filename = target_filename_without_ext + Path.GetExtension(metadata.FullName);
            // 目标全路径
            var target_movie_file = Path.GetFullPath(Path.Combine(target_dir, target_filename));

            // 文件名中可能包含路基，所以需要重新计算文件名
            target_filename = target_movie_file.GetFileName();
            target_filename_without_ext = target_filename.GetFileNameWithoutExtension();
            target_dir = target_movie_file.GetDirectoryName();

            if (hasChineseSubtitle && options.AddChineseSubtitleSuffix >= 1 && options.AddChineseSubtitleSuffix <= 3) // 中文字幕
            {
                if (options.AddChineseSubtitleSuffix == 1 || options.AddChineseSubtitleSuffix == 3)
                {
                    // 包含在文件夹中
                    target_dir += "-C";
                }

                if (options.AddChineseSubtitleSuffix == 2 || options.AddChineseSubtitleSuffix == 3)
                {
                    // 包含在文件名中
                    target_filename_without_ext += "-C";
                }

                target_filename = target_filename_without_ext + Path.GetExtension(target_filename);
                target_movie_file = Path.GetFullPath(Path.Combine(target_dir, target_filename));
            }

            if (!_fileSystem.DirectoryExists(target_dir))
            {
                Directory.CreateDirectory(target_dir);
            }

            // 老的文件名
            var source_name = metadata.FullName.GetFileNameWithoutExtension();
            var source_dir = metadata.FullName.GetDirectoryName();

            // 已经存在的就跳过
            if (!options.OverwriteExistingFiles && _fileSystem.FileExists(target_movie_file))
            {
                _logger.LogError("target directory contains the file with the same name: {}", target_movie_file);
                return false;
            }

            var source_files = _fileSystem.GetFiles(source_dir);
            var pending_files = new List<(string From, string To)>();
            foreach (var path in source_files.Select(o => o.FullName))
            {
                var fileName = Path.GetFileName(path);
                if (fileName.StartsWith(source_name, StringComparison.OrdinalIgnoreCase))
                {
                    fileName = string.Concat(target_filename_without_ext, fileName.AsSpan(source_name.Length));
                    pending_files.Add((path, Path.Combine(target_dir, fileName)));
                }
                else if (fileName.StartsWith("fanart", StringComparison.OrdinalIgnoreCase) || fileName.StartsWith("poster", StringComparison.OrdinalIgnoreCase) || fileName.StartsWith("clearart", StringComparison.OrdinalIgnoreCase))
                {
                    pending_files.Add((path, Path.Combine(target_dir, fileName)));
                }
            }

            foreach (var (from, to) in pending_files)
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
                var source_extrafanart = Path.Combine(source_dir, "extrafanart");
                var target_extrafanart = Path.Combine(target_dir, "extrafanart");
                if (Directory.Exists(source_extrafanart) && (options.OverwriteExistingFiles || !Directory.Exists(target_extrafanart)))
                {
                    if (options.CopyOriginalFile)
                    {
                        if (!_fileSystem.DirectoryExists(target_extrafanart))
                        {
                            Directory.CreateDirectory(target_extrafanart);
                        }

                        foreach (var f in _fileSystem.GetFiles(source_extrafanart))
                        {
                            File.Copy(f.FullName, Path.Combine(target_extrafanart, f.Name), options.OverwriteExistingFiles);
                        }

                        _logger.LogInformation("DirectoryCopy. source:{} target:{}", source_extrafanart, target_extrafanart);
                    }
                    else
                    {
                        Directory.Move(source_extrafanart, target_extrafanart);
                        _logger.LogInformation("DirectoryMove. source:{} target:{}", source_extrafanart, target_extrafanart);
                    }
                }
            }
            catch
            {
            }

            // 更新 nfo 文件
            foreach (var nfo in pending_files.Where(o => o.To.EndsWith(".nfo", StringComparison.OrdinalIgnoreCase)).Select(o => o.To))
            {
                var txt = File.ReadAllText(nfo);
                if (txt.Contains(source_dir, StringComparison.OrdinalIgnoreCase))
                {
                    txt = txt.Replace(source_dir, target_dir, StringComparison.OrdinalIgnoreCase);
                    File.WriteAllText(nfo, txt);
                }
            }

            movie.Path = target_movie_file;
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
                    _logger.LogError(ex, "Error deleting file {}", file);
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

        private bool IsValidWatchLocation(string path, List<string> libraryFolderPaths)
        {
            if (!IsPathAlreadyInMediaLibrary(path, libraryFolderPaths))
            {
                _logger.LogInformation("Folder {} is not eligible for jav-organize because it is not part of an Jellyfin library", path);
                return false;
            }

            return true;
        }

        private bool IsPathAlreadyInMediaLibrary(string path, List<string> libraryFolderPaths)
        {
            return libraryFolderPaths.Any(i => string.Equals(i, path, StringComparison.Ordinal) || _fileSystem.ContainsSubPath(i, path));
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
                _logger.LogInformation("Auto-Organize watch folder does not exist: {}", path);

                return new List<FileSystemMetadata>(0);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Error getting files from {}", path);

                return new List<FileSystemMetadata>(0);
            }
        }
    }
}
