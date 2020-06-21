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

#if __JELLYFIN__
using Microsoft.Extensions.Logging;
#else

using MediaBrowser.Model.Logging;

#endif

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
        private readonly IProviderManager providerManager;
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
            IFileSystem fileSystem)
        {
            _logger = logManager.CreateLogger<JavOrganizeTask>();
            this.libraryManager = libraryManager;
            this._jsonSerializer = _jsonSerializer;
            this.providerManager = providerManager;
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