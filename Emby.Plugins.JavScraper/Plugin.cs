using Emby.Plugins.JavScraper.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;

#if __JELLYFIN__
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Emby.Plugins.JavScraper.Services;
#else
using MediaBrowser.Model.Logging;
#endif

using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using Emby.Plugins.JavScraper.Data;
using Emby.Plugins.JavScraper.Scrapers;
using System.Linq;
using System.Collections.ObjectModel;
using MediaBrowser.Common;

namespace Emby.Plugins.JavScraper
{
    public class Plugin
            : BasePlugin<PluginConfiguration>, IHasWebPages
#if !__JELLYFIN__
        , IHasThumbImage
#endif
    {
        /// <summary>
        /// 名称
        /// </summary>
        public const string NAME = "JavScraper";

        private ILogger logger;

        /// <summary>
        /// 数据库
        /// </summary>
        public ApplicationDbContext db { get; }

        /// <summary>
        /// 全部的刮削器
        /// </summary>
        public ReadOnlyCollection<AbstractScraper> Scrapers { get; }

#if __JELLYFIN__

        /// <summary>
        /// 图片服务
        /// </summary>
        public ImageProxyService ImageProxyService { get; }

        /// <summary>
        /// 翻译服务
        /// </summary>
        public TranslationService TranslationService { get; }

#endif

        /// <summary>
        /// COPY TO /volume1/@appstore/EmbyServer/releases/4.3.1.0/plugins
        /// </summary>
        /// <param name="applicationPaths"></param>
        /// <param name="xmlSerializer"></param>
        /// <param name="logger"></param>
        public Plugin(IApplicationPaths applicationPaths, IApplicationHost applicationHost, IXmlSerializer xmlSerializer,
#if __JELLYFIN__
            IServiceProvider serviceProvider,
            ILoggerFactory logManager
#else
            ILogManager logManager
#endif
            ) : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            logger = logManager.CreateLogger<Plugin>();
            logger?.Info($"{Name} - Loaded.");
            db = ApplicationDbContext.Create(applicationPaths);
            Scrapers = applicationHost.GetExports<AbstractScraper>(false).Where(o => o != null).ToList().AsReadOnly();

#if __JELLYFIN__
            ImageProxyService = new ImageProxyService(serviceProvider.GetService<IJsonSerializer>(), logManager.CreateLogger<ImageProxyService>(),
                serviceProvider.GetService<MediaBrowser.Model.IO.IFileSystem>(), applicationPaths);
            TranslationService = new TranslationService(serviceProvider.GetService<IJsonSerializer>(), logManager.CreateLogger<TranslationService>());
#endif
        }

        public override Guid Id => new Guid("0F34B81A-4AF7-4719-9958-4CB8F680E7C6");

        public override string Name => NAME;

        public override string Description => "Jav Scraper";

        public static Plugin Instance { get; private set; }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            var type = GetType();
            string prefix = "";
#if __JELLYFIN__
            prefix = "Jellyfin.";
#endif
            return new[]
            {
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = $"{type.Namespace}.Configuration.{prefix}ConfigPage.html",
                    EnableInMainMenu = true,
                    MenuSection = "server",
                    MenuIcon = "theaters",
                    DisplayName = "Jav Scraper",
                },
                new PluginPageInfo
                {
                    Name = "JavOrganize",
                    EmbeddedResourcePath = $"{type.Namespace}.Configuration.{prefix}JavOrganizationConfigPage.html",
                    EnableInMainMenu = true,
                    MenuSection = "server",
                    MenuIcon = "theaters",
                    DisplayName = "Jav Organize",
                }
            };
        }

        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream($"{type.Namespace}.thumb.png");
        }

        public ImageFormat ThumbImageFormat => ImageFormat.Png;

        public override void SaveConfiguration()
        {
            Configuration.ConfigurationVersion = DateTime.Now.Ticks;
            base.SaveConfiguration();
        }
    }
}