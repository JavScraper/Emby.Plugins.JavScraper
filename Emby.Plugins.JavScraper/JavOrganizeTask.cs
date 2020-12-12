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
    public class JavOrganizeTask : IScheduledTask
    {
        public string Name { get; } = "JavOrganize: 立即整理日本电影文件<span style='color:#FF0000;'>【实验功能】</span>";
        public string Key { get; } = "JavOrganize";
        public string Description { get; } = "立即整理日本电影文件，使用之前请先<a data-navmenuid='/configurationpage?name=JavOrganize' is='emby-linkbutton' class='button-link emby-button' href='configurationpage?name=JavOrganize' title='配置'>配置</a>规则。<br /><span style='color:#FF0000;'>该功能目前尚处于实验阶段，请谨慎使用及做好数据备份。</span><span style='color:#FF0000;'>由此插件引起的数据丢失或其他任何问题，作者不负任何责任。</span>";
        public string Category => "JavScraper";

        private readonly ILibraryManager _libraryManager;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IApplicationPaths appPaths;
        private readonly IProviderManager providerManager;
        private readonly ILibraryMonitor libraryMonitor;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;

        public JavOrganizeTask(
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
            _logger = logManager.CreateLogger<JavOrganizeTask>();
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
            _logger.Info($"Running...");
            progress.Report(0);

            var options = Plugin.Instance.Configuration?.JavOrganizationOptions;
            var empty = Plugin.Instance.Configuration?.TitleFormatEmptyValue;

            if (options?.WatchLocations?.Any() != true && string.IsNullOrWhiteSpace(options.WatchLocations[0]))
            {
                _logger.Warn("source folder cannot be empty.");
                return;
            }

            if (string.IsNullOrWhiteSpace(options?.TargetLocation))
            {
                _logger.Warn("target folder is empty.");
                return;
            }

            if (string.IsNullOrWhiteSpace(options.MovieFolderPattern) && string.IsNullOrWhiteSpace(options.MoviePattern))
            {
                _logger.Warn("folder pattern and file name pattern cannot be empty at the same time.");
                return;
            }

            var libraryFolderPaths = _libraryManager.GetVirtualFolders()
                .Where(dir => dir.CollectionType == "movies" && dir.Locations?.Any() == true &&
                    dir.LibraryOptions.TypeOptions?.Any(o => o.MetadataFetchers?.Contains(Plugin.NAME) == true) == true)
                .SelectMany(o => o.Locations).ToList();

            var watchLocations = options.WatchLocations
                .Where(o => IsValidWatchLocation(o, libraryFolderPaths))
                .ToList();

            var eligibleFiles = watchLocations.SelectMany(GetFilesToOrganize)
                .OrderBy(_fileSystem.GetCreationTimeUtc)
                .Where(i => EnableOrganization(i, options))
                .ToList();

            var processedFolders = new HashSet<string>();

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
                    var r = Do(options, empty, m);
                    if (r && !processedFolders.Contains(m.DirectoryName, StringComparer.OrdinalIgnoreCase))
                        processedFolders.Add(m.DirectoryName);
                }
                catch (Exception ex)
                {
                    _logger.Error($"{m.FullName}  {ex.Message}");
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

        private bool Do(JavOrganizationOptions options, string empty, FileSystemMetadata m)
        {
            var movie = _libraryManager.FindByPath(m.FullName, false) as Movie;
            if (movie == null)
            {
                _logger.Error($"the movie does not exists. {m.FullName}");
                return false;
            }

            var jav = movie.GetJavVideoIndex(_jsonSerializer);
            if (jav == null)
            {
                _logger.Error($"jav video index does not exists. {m.FullName}");
                return false;
            }

            #region 尝试还原 JavVideoIndex

            if (jav.Genres == null || jav.Actors == null)
            {
                var l = Plugin.Instance.db.FindJavVideo(jav.Provider, jav.Url);
                if (l != null)
                    jav = l;
            }

            if (jav?.Genres?.Any() != true && movie.Genres?.Any() == true)
                jav.Genres = movie.Genres.ToList();

            if (jav?.Actors?.Any() != true || string.IsNullOrWhiteSpace(jav.Director))
            {
#if __JELLYFIN__
                var persons = _libraryManager.GetPeople(movie);
#else
                var persons = _libraryManager.GetItemPeople(movie);
#endif

                if (persons?.Any() == true)
                {
                    if (jav?.Actors?.Any() != true)
                    {
                        jav.Actors = persons.Where(o => o.Type == MediaBrowser.Model.Entities.PersonType.Actor)
                            .Select(o => o.Name)
                            .ToList();
                    }
                    if (string.IsNullOrWhiteSpace(jav.Director))
                        jav.Director = persons.Where(o => o.Type == MediaBrowser.Model.Entities.PersonType.Director)
                            .Select(o => o.Name)
                            .FirstOrDefault();
                }
            }

            if (string.IsNullOrWhiteSpace(jav.Studio) && movie.Studios?.Any() == true)
                jav.Studio = movie.Studios?.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(jav.Plot) && !string.IsNullOrWhiteSpace(movie.Overview))
                jav.Plot = movie.Overview;

            if (string.IsNullOrWhiteSpace(jav.OriginalTitle) && !string.IsNullOrWhiteSpace(movie.OriginalTitle))
                jav.OriginalTitle = movie.OriginalTitle;

            if (string.IsNullOrWhiteSpace(jav.Set) && !string.IsNullOrWhiteSpace(movie.CollectionName))
                jav.Set = movie.CollectionName;

            if (jav.Date == null && movie.PremiereDate != null)
                jav.Date = movie.PremiereDate.Value.Date.ToString("yyyy-MM-dd");

            if (jav.Date == null && movie.ProductionYear > 0)
                jav.Date = $"{movie.ProductionYear}-01-01";

            #endregion 尝试还原 JavVideoIndex

            //1，文件名中可能包含路径，
            //2，去除路径中非法字符
            //3，路径分隔符
            //4，文件夹或者文件名中包含-C/-C2 中文字幕
            //5，移动以文件名开通的文件
            //6，移动某些特定文件名的文件
            //7，替换nfo文件内的路径
            //8，复制nfo中的其他文件?

            var has_chinese_subtitle = movie.Genres?.Contains("中文字幕") == true;
            if (has_chinese_subtitle == false)
            {
                var arr = new[] { Path.GetFileNameWithoutExtension(m.FullName), Path.GetFileName(Path.GetDirectoryName(m.FullName)) };
                var cc = new[] { "-C", "-C2", "_C", "_C2" };
                has_chinese_subtitle = arr.Any(v => cc.Any(x => v.EndsWith(x, StringComparison.OrdinalIgnoreCase)));
            }

            var target_dir = options.TargetLocation;
            if (string.IsNullOrWhiteSpace(options.MovieFolderPattern) == false)
                target_dir = Path.Combine(target_dir, jav.GetFormatName(options.MovieFolderPattern, empty, true));
            string target_name = null;
            if (string.IsNullOrWhiteSpace(options.MoviePattern) == false)
            {
                //文件名部分
                target_name = jav.GetFormatName(options.MoviePattern, empty, true);
            }
            else
            {
                target_name = Path.GetFileName(target_dir);
                target_dir = Path.GetDirectoryName(target_dir);
            }
            //文件名（含扩展名）
            var target_filename = target_name + Path.GetExtension(m.FullName);
            //目标全路径
            var target_movie_file = Path.GetFullPath(Path.Combine(target_dir, target_filename));

            //文件名中可能包含路基，所以需要重新计算文件名
            target_filename = Path.GetFileName(target_movie_file);
            target_name = Path.GetFileNameWithoutExtension(target_filename);
            target_dir = Path.GetDirectoryName(target_movie_file);

            if (has_chinese_subtitle && options.AddChineseSubtitleSuffix >= 1 && options.AddChineseSubtitleSuffix <= 3) //中文字幕
            {
                if (options.AddChineseSubtitleSuffix == 1 || options.AddChineseSubtitleSuffix == 3)
                    //包含在文件夹中
                    target_dir += "-C";
                if (options.AddChineseSubtitleSuffix == 2 || options.AddChineseSubtitleSuffix == 3)
                    //包含在文件名中
                    target_name += "-C";
                target_filename = target_name + Path.GetExtension(target_filename);
                target_movie_file = Path.GetFullPath(Path.Combine(target_dir, target_filename));
            }

            if (_fileSystem.DirectoryExists(target_dir) == false)
                _fileSystem.CreateDirectory(target_dir);

            //老的文件名
            var source_name = Path.GetFileNameWithoutExtension(m.FullName);
            var source_dir = Path.GetDirectoryName(m.FullName);

            //已经存在的就跳过
            if (options.OverwriteExistingFiles == false && _fileSystem.FileExists(target_movie_file))
            {
                _logger.Error($"FileExists: {target_movie_file}");
                return false;
            }
            var source_files = _fileSystem.GetFiles(source_dir);
            var pending_files = new List<(string from, string to)>();
            foreach (var f in source_files.Select(o => o.FullName))
            {
                var n = Path.GetFileName(f);
                if (n.StartsWith(source_name, StringComparison.OrdinalIgnoreCase))
                {
                    n = target_name + n.Substring(source_name.Length);
                    pending_files.Add((f, Path.Combine(target_dir, n)));
                }
                else if (n.StartsWith("fanart", StringComparison.OrdinalIgnoreCase) || n.StartsWith("poster", StringComparison.OrdinalIgnoreCase) || n.StartsWith("clearart", StringComparison.OrdinalIgnoreCase))
                    pending_files.Add((f, Path.Combine(target_dir, n)));
            }

            foreach (var f in pending_files)
            {
                if (options.OverwriteExistingFiles == false && _fileSystem.FileExists(f.to))
                {
                    _logger.Info($"FileSkip: {f.from} {f.to}");
                    return false;
                }

                if (options.CopyOriginalFile)
                {
                    _fileSystem.CopyFile(f.from, f.to, options.OverwriteExistingFiles);
                    _logger.Info($"FileCopy: {f.from} {f.to}");
                }
                else
                {
                    _fileSystem.MoveFile(f.from, f.to);
                    _logger.Info($"FileMove: {f.from} {f.to}");
                }
            }

            try
            {
                //非必须的
                var source_extrafanart = Path.Combine(source_dir, "extrafanart");
                var target_extrafanart = Path.Combine(target_dir, "extrafanart");
                if (Directory.Exists(source_extrafanart) && (options.OverwriteExistingFiles || !Directory.Exists(target_extrafanart)))
                {
                    if (options.CopyOriginalFile)
                    {
                        if (_fileSystem.DirectoryExists(target_extrafanart) == false)
                            _fileSystem.CreateDirectory(target_extrafanart);

                        foreach (var f in _fileSystem.GetFiles(source_extrafanart))
                            _fileSystem.CopyFile(f.FullName, Path.Combine(target_extrafanart, f.Name), options.OverwriteExistingFiles);

                        _logger.Info($"DirectoryCopy: {source_extrafanart} {target_extrafanart}");
                    }
                    else
                    {
                        _fileSystem.MoveDirectory(source_extrafanart, target_extrafanart);
                        _logger.Info($"DirectoryMove: {source_extrafanart} {target_extrafanart}");
                    }
                }
            }
            catch { }

            //更新 nfo 文件
            foreach (var nfo in pending_files.Where(o => o.to.EndsWith(".nfo", StringComparison.OrdinalIgnoreCase)).Select(o => o.to))
            {
                var txt = File.ReadAllText(nfo);
                if (txt.IndexOf(source_dir) >= 0)
                {
                    txt = txt.Replace(source_dir, target_dir);
                    File.WriteAllText(nfo, txt);
                }
            }
            movie.Path = target_movie_file;
            movie.UpdateToRepository(ItemUpdateType.MetadataImport);
            return true;
        }

        private bool EnableOrganization(FileSystemMetadata fileInfo, JavOrganizationOptions options)
        {
            var minFileBytes = options.MinFileSizeMb * 1024 * 1024;

            try
            {
                return _libraryManager.IsVideoFile(fileInfo.FullName
#if !__JELLYFIN__
                    .AsSpan()
#endif
                    ) && fileInfo.Length >= minFileBytes;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error organizing file {fileInfo.Name}: {ex.Message}");
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
            var eligibleFiles = _fileSystem.GetFilePaths(path, extensions.ToArray(), false, true)
                .ToList();

            foreach (var file in eligibleFiles)
            {
                try
                {
                    _fileSystem.DeleteFile(file);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error deleting file {file}: {ex.Message}");
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

                if (!entries.Any() && !IsWatchFolder(path, watchLocations))
                {
                    try
                    {
                        _logger.Debug($"Deleting empty directory {path}");
                        _fileSystem.DeleteDirectory(path, false);
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
        }

        /// <summary>
        /// Determines if a given folder path is contained in a folder list
        /// </summary>
        /// <param name="path">The folder path to check.</param>
        /// <param name="watchLocations">A list of folders.</param>
        private bool IsWatchFolder(string path, IEnumerable<string> watchLocations)
        {
            return watchLocations.Contains(path, StringComparer.OrdinalIgnoreCase);
        }

        private bool IsValidWatchLocation(string path, List<string> libraryFolderPaths)
        {
            if (!IsPathAlreadyInMediaLibrary(path, libraryFolderPaths))
            {
                _logger.Info($"Folder {path} is not eligible for jav-organize because it is not part of an Emby library");
                return false;
            }

            return true;
        }

        private bool IsPathAlreadyInMediaLibrary(string path, List<string> libraryFolderPaths)
        {
            return libraryFolderPaths.Any(i => string.Equals(i, path, StringComparison.Ordinal) || _fileSystem.ContainsSubPath(i
#if !__JELLYFIN__
                .AsSpan()
#endif
                , path
#if !__JELLYFIN__
                .AsSpan()
#endif
                ));
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
                return _fileSystem.GetFiles(path, true)
                    .ToList();
            }
            catch (DirectoryNotFoundException)
            {
                _logger.Info($"Auto-Organize watch folder does not exist: {path}");

                return new List<FileSystemMetadata>();
            }
            catch (IOException ex)
            {
                _logger.Error($"Error getting files from {path}: {ex.Message}");

                return new List<FileSystemMetadata>();
            }
        }
    }
}