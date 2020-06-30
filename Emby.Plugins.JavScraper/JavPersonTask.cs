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
using Emby.Plugins.JavScraper.Scrapers;
using MediaBrowser.Model.Serialization;
using Emby.Plugins.JavScraper.Services;
using MediaBrowser.Common.Configuration;

#if __JELLYFIN__
using Microsoft.Extensions.Logging;
#else
using MediaBrowser.Model.Logging;
#endif

namespace Emby.Plugins.JavScraper
{
    public class JavPersonTask : IScheduledTask
    {
        private readonly ILibraryManager libraryManager;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IProviderManager providerManager;
        private readonly IFileSystem fileSystem;
        private readonly ILogger _logger;

        public Gfriends Gfriends { get; }
        public ImageProxyService ImageProxyService => Plugin.Instance.ImageProxyService;

        public JavPersonTask(
#if __JELLYFIN__
            ILoggerFactory logManager
#else
            ILogManager logManager
#endif
            , ILibraryManager libraryManager, IJsonSerializer _jsonSerializer, IApplicationPaths appPaths,
            IProviderManager providerManager,
            IFileSystem fileSystem)
        {
            _logger = logManager.CreateLogger<JavPersonTask>();
            this.libraryManager = libraryManager;
            this._jsonSerializer = _jsonSerializer;
            this.providerManager = providerManager;
            this.fileSystem = fileSystem;
            Gfriends = new Gfriends(logManager.CreateLogger<Gfriends>(), _jsonSerializer);
        }

        public string Name => Plugin.NAME + ": 采集缺失的女优头像";
        public string Key => Plugin.NAME + "-Actress";
        public string Description => "采集缺失的女优头像";
        public string Category => "JavScraper";

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

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
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
            var type = ImageType.Primary;
            var enable = Plugin.Instance?.Configuration?.EnableCutPersonImage ?? true;
            var image_type = enable ? type : ImageType.Backdrop;
            //persons.RemoveAll(o => o.ImageInfos?.Any(v => v.IsLocalFile == true && v.Type == type) == true);

            for (int i = 0; i < persons.Count; ++i)
            {
                var person = persons[i];
                var url = await Gfriends.FindAsync(person.Name, cancellationToken);
                do
                {
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        _logger.Info($"{person.Name} not found.");
                        break;
                    }
                    var index = person.GetJavPersonIndex(_jsonSerializer);

                    if (index?.Url == url && index.ImageType == image_type && person.ImageInfos?.Any(v => v.IsLocalFile == true && v.Type == type) == true)
                    {
                        _logger.Info($"{person.Name} already exist.");
                        break;
                    }

                    var resp = await ImageProxyService.GetImageResponse(url, image_type, cancellationToken);
                    if (resp?.ContentLength > 0)
                    {
#if __JELLYFIN__
                        await providerManager.SaveImage(person, resp.Content, resp.ContentType, type, 0, cancellationToken);
#else
                        await providerManager.SaveImage(person, libraryManager.GetLibraryOptions(person), resp.Content, resp.ContentType.ToArray(), type, 0, cancellationToken);
#endif

                        index = new JavPersonIndex() { Provider = Gfriends.Name, Url = url, ImageType = image_type };
                        person.SetJavPersonIndex(_jsonSerializer, index);
                        person.UpdateToRepository(ItemUpdateType.MetadataEdit);
                        _logger.Info($"saved image: {person.Name} {url}");
                    }
                } while (false);
                progress.Report(i * 1.0 / persons.Count * 100);
            }

            progress.Report(100);
        }
    }
}