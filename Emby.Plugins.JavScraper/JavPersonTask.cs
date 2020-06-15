using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using System.Collections.Generic;

#if __JELLYFIN__
using Microsoft.Extensions.Logging;
#else
using MediaBrowser.Model.Logging;
#endif

namespace Emby.Plugins.JavScraper
{
    public class JavPersonMetadataTask : ILibraryPostScanTask, IScheduledTask
    {
        private readonly ILibraryManager libraryManager;
        private readonly IFileSystem fileSystem;
        private readonly ILogger _logger;

        public JavPersonMetadataTask(
#if __JELLYFIN__
            ILoggerFactory logManager
#else
            ILogManager logManager
#endif
            , ILibraryManager libraryManager,
            IFileSystem fileSystem)
        {
            _logger = logManager.CreateLogger<JavPersonMetadataTask>();
            this.libraryManager = libraryManager;
            this.fileSystem = fileSystem;
        }

        public string Name => Plugin.NAME + " 采集女优头像";
        public string Key => Plugin.NAME + "-Actress";
        public string Description => "采集女优头像";
        public string Category => "Library";

        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            return Run(progress, cancellationToken);
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            var t = new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerWeekly,
                TimeOfDayTicks = TimeSpan.FromHours(2).Ticks,
                MaxRuntimeTicks = TimeSpan.FromHours(3).Ticks,
                DayOfWeek = DayOfWeek.Monday
            };
            return new[] { t };
        }

        public async Task Run(IProgress<double> progress,
                              CancellationToken cancellationToken)
        {
            _logger.Info($"Running...");
            progress.Report(0);

            var options = new MetadataRefreshOptions(
                new DirectoryService(fileSystem)
            )
            {
                ImageRefreshMode = MetadataRefreshMode.FullRefresh,
                MetadataRefreshMode = MetadataRefreshMode.FullRefresh
            };

            var query = new InternalItemsQuery()
            {
                IncludeItemTypes = new[] { nameof(Person) },
                PersonTypes = new[] { PersonType.Actor }
            };

            var persons = libraryManager.GetItemList(query)?.ToList();

            if (persons?.Any() != true)
            {
                progress.Report(100);
                return;
            }
            persons.RemoveAll(o => !(o is Person));
            //移除已经存在头像的
            persons.RemoveAll(o => o.ImageInfos?.Any(v => v.IsLocalFile == true && v.Type == ImageType.Primary) == true);

            for (int i = 0; i < persons.Count; ++i)
            {
                var person = persons[i];
                _logger.Info($"{person.Name}");
                await person.RefreshMetadata(options, cancellationToken);

                progress.Report(i / persons.Count * 100);
            }

            progress.Report(100);
        }
    }
}