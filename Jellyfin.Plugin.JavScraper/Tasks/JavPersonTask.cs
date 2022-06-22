using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Tasks
{
    public class JavPersonTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private readonly IDirectoryService _directoryService;

        public JavPersonTask(
            ILoggerFactory loggerFactory,
            ILibraryManager libraryManager,
            IFileSystem fileSystem)
        {
            _logger = loggerFactory.CreateLogger<JavPersonTask>();
            _libraryManager = libraryManager;
            _directoryService = new DirectoryService(fileSystem);
        }

        public string Name => $"{Constants.PluginName}: 采集缺失的女优头像和信息";

        public string Key => $"{Constants.PluginName}-Actress";

        public string Description => "采集缺失的女优头像和信息";

        public string Category => Constants.PluginName;

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            var triggerInfo = new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerWeekly,
                TimeOfDayTicks = TimeSpan.FromHours(2).Ticks,
                MaxRuntimeTicks = TimeSpan.FromHours(3).Ticks,
                DayOfWeek = DayOfWeek.Monday
            };
            return new[] { triggerInfo };
        }

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Running...");
            progress.Report(0);

            var query = new InternalItemsQuery()
            {
                IncludeItemTypes = new[] { BaseItemKind.Person },
                PersonTypes = new[] { PersonType.Actor }
            };

            var persons = _libraryManager.GetItemList(query)?.Where(person => person is Person).ToList();

            if (persons == null || !persons.Any())
            {
                progress.Report(100);
                return;
            }

            for (var i = 0; i < persons.Count; ++i)
            {
                var person = persons[i];

                MetadataRefreshMode imageRefreshMode = 0;
                MetadataRefreshMode metadataRefreshMode = 0;

                if (!person.HasImage(ImageType.Primary))
                {
                    imageRefreshMode = MetadataRefreshMode.Default;
                }

                if (string.IsNullOrEmpty(person.Overview))
                {
                    metadataRefreshMode = MetadataRefreshMode.FullRefresh;
                }

                if (imageRefreshMode == 0 && metadataRefreshMode == 0)
                {
                    continue;
                }

                var options = new MetadataRefreshOptions(_directoryService)
                {
                    ImageRefreshMode = imageRefreshMode,
                    MetadataRefreshMode = metadataRefreshMode
                };

                await person.RefreshMetadata(options, cancellationToken).ConfigureAwait(false);
                progress.Report(i * 1.0 / persons.Count * 100);
            }

            progress.Report(100);
        }
    }
}
