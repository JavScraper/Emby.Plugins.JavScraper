using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.JavScraper.Configuration;
using Jellyfin.Plugin.JavScraper.Extensions;
using Jellyfin.Plugin.JavScraper.Http;
using Jellyfin.Plugin.JavScraper.Scrapers;
using Jellyfin.Plugin.JavScraper.Services;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        private readonly ILogger _logger;

        public Plugin(
            IApplicationPaths applicationPaths,
            IApplicationHost applicationHost,
            IXmlSerializer xmlSerializer,
            ILoggerFactory loggerFactory,
            BodyAnalysisService bodyAnalysisService,
            JavWebProxy proxy) : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            _logger = loggerFactory.CreateLogger<Plugin>();
            _logger.LogInformation("{Name} - Loaded.", Name);
            var serviceCollection = new ServiceCollection();
            Scrapers = applicationHost.GetExports<AbstractScraper>().Where(o => o != null).ToList().ToArray();
            _logger.LogInformation("{Count} scrapers loaded.", Scrapers.Count);
            proxy.Reset(Configuration);
            ConfigurationChanged = (sender, e) =>
            {
                if (e is not PluginConfiguration config)
                {
                    return;
                }

                _logger.LogInformation("Configuration change, refresh services.");
                // update bodyAnalysisService config
                bodyAnalysisService.RefreshToken(config.BaiduBodyAnalysisApiKey, config.BaiduBodyAnalysisSecretKey);
                // update scraper config
                /*var scraperConfigMap = config.ScraperConfigList.ToDictionary(scraperConfig => scraperConfig.Name);
                foreach (AbstractScraper scraper in Scrapers)
                {
                    var scraperConfig = scraperConfigMap[scraper.Name];
                    if (scraperConfig.Url.IsWebUrl())
                    {
                        scraper.BaseAddress = scraperConfig.Url;
                    }
                    else
                    {
                        scraperConfig.Url = scraper.DefaultBaseUrl;
                    }
                }*/

                proxy.Reset(config);
            };
        }

        /// <summary>
        /// 全部的刮削器
        /// </summary>
        public IReadOnlyList<AbstractScraper> Scrapers { get; }

        public override Guid Id => new("0F34B81A-4AF7-4719-9958-4CB8F680E7C6");

        public override string Name => Constants.PluginName;

        public override string Description => "Jav Scraper";

        /// <summary>
        /// Gets the current plugin instance.
        /// </summary>
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
        public static Plugin Instance { get; private set; }
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。

        public static ImageFormat ThumbImageFormat => ImageFormat.Png;

        public IEnumerable<PluginPageInfo> GetPages()
        {
            var type = GetType();
            return new[]
            {
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = $"{type.Namespace}.Configuration.Jellyfin.ConfigPage.html",
                    EnableInMainMenu = true,
                    MenuSection = "server",
                    MenuIcon = "theaters",
                    DisplayName = "Jav Scraper",
                },
                new PluginPageInfo
                {
                    Name = "JavOrganize",
                    EmbeddedResourcePath = $"{type.Namespace}.Configuration.Jellyfin.JavOrganizationConfigPage.html",
                    EnableInMainMenu = true,
                    MenuSection = "server",
                    MenuIcon = "theaters",
                    DisplayName = "Jav Organize",
                }
            };
        }

        public Stream? GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream($"{type.Namespace}.thumb.png");
        }

        public override void SaveConfiguration()
        {
            Configuration.ConfigurationVersion = DateTime.Now.Ticks;
            base.SaveConfiguration();
        }
    }
}
