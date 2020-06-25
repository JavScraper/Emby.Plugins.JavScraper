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

#if __JELLYFIN__
using Microsoft.Extensions.Logging;
#else

using MediaBrowser.Model.Logging;

#endif

#if DEBUG

namespace Emby.Plugins.JavScraper
{
    public class JavOrganizeTask : IScheduledTask
    {
        public string Name { get; } = "JavOrganize";
        public string Key { get; } = "JavOrganize";
        public string Description { get; } = "JavOrganize";
        public string Category => "Library";

        private readonly ILibraryManager libraryManager;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IApplicationPaths appPaths;
        private readonly IProviderManager providerManager;
        private readonly ILibraryMonitor libraryMonitor;
        private readonly IFileSystem fileSystem;
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
            this.libraryManager = libraryManager;
            this._jsonSerializer = _jsonSerializer;
            this.appPaths = appPaths;
            this.providerManager = providerManager;
            this.libraryMonitor = libraryMonitor;
            this.fileSystem = fileSystem;
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
            => new TaskTriggerInfo[] { };

        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var dirs = libraryManager.GetVirtualFolders();
            foreach (var dir in dirs)
            {
                if (dir.CollectionType == "movies" && dir.Locations?.Any() == true &&
                    dir.LibraryOptions.TypeOptions?.Any(o => o.MetadataFetchers?.Contains(Plugin.NAME) == true) == true)
                {
                    foreach (var path in dir.Locations)
                    {
                        var files = GetFilesToOrganize(path)
                            .OrderBy(fileSystem.GetCreationTimeUtc)
                            .Where(i => EnableOrganization(i))
                            .ToList();

                        foreach (var m in files)
                        {
                            var movie = libraryManager.FindByPath(m.FullName, false) as Movie;
                            if (movie == null)
                            {
                                _logger.Error("movie null");
                                continue;
                            }

                            var jav = movie.GetJavVideoIndex(_jsonSerializer);
                            if (jav == null)
                            {
                                _logger.Error("jav null");
                                continue;
                            }

                            if (jav.Genres == null)
                            {
                                var l = jav.LoadFromCache(appPaths.CachePath, _jsonSerializer);
                                if (l != null)
                                    jav = l;
                            }

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

                            var target_dir = Path.Combine(path, "Output", jav.GetFormatName(@"%actor%/%num%", "NULL", true));
                            //文件名部分
                            var name = jav.GetFormatName("%num%", "NULL", true);
                            //文件名（含扩展名）
                            var filename = name + Path.GetExtension(m.FullName);
                            //目标全路径
                            var full = Path.GetFullPath(Path.Combine(target_dir, filename));

                            //文件名中可能包含路基，所以需要重新计算文件名
                            filename = Path.GetFileName(full);
                            name = Path.GetFileNameWithoutExtension(filename);
                            target_dir = Path.GetDirectoryName(full);

                            if (has_chinese_subtitle) //中文字幕
                            {
                                //包含在文件夹中
                                target_dir += "-C";
                                //包含在文件名中
                                name += "-C";
                                filename = name + Path.GetExtension(filename);
                                full = Path.GetFullPath(Path.Combine(target_dir, filename));
                            }

                            if (fileSystem.DirectoryExists(target_dir) == false)
                                fileSystem.CreateDirectory(target_dir);

                            //老的文件名
                            var old_name = Path.GetFileNameWithoutExtension(m.FullName);
                            var old_dir = Path.GetDirectoryName(m.FullName);

                            if (fileSystem.FileExists(full))
                            {
                                _logger.Error($"FileExists: {full}");
                                continue;
                            }
                            var old_files = fileSystem.GetFiles(old_dir);
                            var fss = new List<(string from, string to)>();
                            foreach (var f in old_files.Select(o => o.FullName))
                            {
                                var n = Path.GetFileName(f);
                                if (n.StartsWith(old_name, StringComparison.OrdinalIgnoreCase))
                                {
                                    n = name + n.Substring(old_name.Length);
                                    fss.Add((f, Path.Combine(target_dir, n)));
                                }
                                else if (n.StartsWith("fanart", StringComparison.OrdinalIgnoreCase) || n.StartsWith("poster", StringComparison.OrdinalIgnoreCase))
                                    fss.Add((f, Path.Combine(target_dir, n)));
                            }

                            foreach (var f in fss.Where(o => !fileSystem.FileExists(o.to)))
                            {
                                fileSystem.MoveFile(f.from, f.to);
                                _logger.Error($"FileMove: {f.from} {f.to}");
                            }

                            var old_extrafanart = Path.Combine(old_dir, "extrafanart");
                            var target_extrafanart = Path.Combine(target_dir, "extrafanart");
                            if (Directory.Exists(old_extrafanart) && !Directory.Exists(target_extrafanart))
                            {
                                fileSystem.MoveDirectory(old_extrafanart, target_extrafanart);
                            }

                            foreach (var nfo in fss.Where(o => o.to.EndsWith(".nfo", StringComparison.OrdinalIgnoreCase)).Select(o => o.to))
                            {
                                var txt = File.ReadAllText(nfo);
                                if (txt.IndexOf(old_dir) >= 0)
                                {
                                    txt = txt.Replace(old_dir, target_dir);
                                    File.WriteAllText(nfo, txt);
                                }
                            }
                            movie.Path = full;
                            movie.UpdateToRepository(ItemUpdateType.MetadataImport);
        
                        }
                    }
                }
            }
            return Task.CompletedTask;
        }

        public Task Execute2(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var dirs = libraryManager.GetVirtualFolders();
            foreach (var dir in dirs)
            {
                if (dir.CollectionType == "movies" && dir.Locations?.Any() == true &&
                    dir.LibraryOptions.TypeOptions?.Any(o => o.MetadataFetchers?.Contains(Plugin.NAME) == true) == true)
                {
                    foreach (var path in dir.Locations)
                    {
                        var files = GetFilesToOrganize(path)
                            .OrderBy(fileSystem.GetCreationTimeUtc)
                            .Where(i => EnableOrganization(i))
                            .ToList();

                        foreach (var ddd in files.GroupBy(o => Path.GetDirectoryName(o.FullName)))
                        {
                            var targetFolder = libraryManager.FindByPath(ddd.Key, true);
                            if (targetFolder == null)
                            {
                                _logger.Error($"targetFolder null: {ddd.Key}");
                                //continue;
                            }

                            if (ddd.Count() == 1) //只有一个视频，很好处理，整个移动过去
                            {
                                var m = ddd.FirstOrDefault();
                                var movie = libraryManager.FindByPath(m.FullName, false);
                                if (movie == null)
                                {
                                    _logger.Error("movie null");
                                    continue;
                                }

                                var jav = movie.GetJavVideoIndex(_jsonSerializer);
                                if (jav == null)
                                {
                                    _logger.Error("jav null");
                                    continue;
                                }

                                if (jav.Genres == null)
                                {
                                    var l = jav.LoadFromCache(appPaths.CachePath, _jsonSerializer);
                                    if (l != null)
                                        jav = l;
                                }

                                var target = Path.Combine(path, "Output", jav.GetFormatName("%actor%/%num%", "NULL"));
                                if (fileSystem.DirectoryExists(target))
                                {
                                    _logger.Error($"dir exists {target}");
                                    continue;
                                }

                                var pp = Path.GetDirectoryName(target);
                                if (fileSystem.DirectoryExists(pp) == false)
                                    fileSystem.CreateDirectory(pp);

                                fileSystem.MoveDirectory(ddd.Key, target);
                                var new_name = Path.GetFileName(target);
                                var old_name = Path.GetFileName(ddd.Key);
                                if (new_name != old_name)
                                {
                                    fileSystem.MoveDirectory(Path.Combine(pp, old_name), Path.Combine(pp, new_name));
                                }
                                libraryMonitor.ReportFileSystemChangeBeginning(target);
                                _logger.Info(_jsonSerializer.SerializeToString(jav));
                            }
                        }
                    }
                }
            }
            return Task.CompletedTask;
        }

        private bool EnableOrganization(FileSystemMetadata fileInfo)
        {
            var minFileBytes = 10;//options.MinFileSizeMb * 1024 * 1024;

            try
            {
                return libraryManager.IsVideoFile(fileInfo.FullName.AsSpan()) && fileInfo.Length >= minFileBytes;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error organizing file {0}", ex, fileInfo.Name);
            }

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
                return fileSystem.GetFiles(path, true)
                    .ToList();
            }
            catch (DirectoryNotFoundException)
            {
                _logger.Info("Auto-Organize watch folder does not exist: {0}", path);

                return new List<FileSystemMetadata>();
            }
            catch (IOException ex)
            {
                _logger.ErrorException("Error getting files from {0}", ex, path);

                return new List<FileSystemMetadata>();
            }
        }
    }
}

#endif