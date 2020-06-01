using Emby.Plugins.JavScraper.Configuration;
using Emby.Plugins.JavScraper.Http;
using Emby.Plugins.JavScraper.Scrapers;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;

#if __JELLYFIN__
using Microsoft.Extensions.Logging;
#else
using MediaBrowser.Model.Logging;
#endif

using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.IO;

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
        /// COPY TO /volume1/@appstore/EmbyServer/releases/4.3.1.0/plugins
        /// </summary>
        /// <param name="applicationPaths"></param>
        /// <param name="xmlSerializer"></param>
        /// <param name="logger"></param>
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer,
#if __JELLYFIN__
            ILoggerFactory logManager
#else
            ILogManager logManager
#endif
            ) : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            logger = logManager.CreateLogger<Plugin>();
            logger?.Info($"{Name} - Loaded.");
        }

        public override Guid Id => new Guid("0F34B81A-4AF7-4719-9958-4CB8F680E7C6");

        public override string Name => NAME;

        public override string Description => "Jav Scraper";

        public static Plugin Instance { get; private set; }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            var type = GetType();
            return new[]
            {
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = $"{type.Namespace}.Configuration.ConfigPage.html",
                    EnableInMainMenu = true,
                    MenuSection = "server",
                    MenuIcon = "theaters",
                    DisplayName = "Jav Scraper",
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