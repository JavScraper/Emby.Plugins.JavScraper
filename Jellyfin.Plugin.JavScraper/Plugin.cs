using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Jellyfin.Plugin.JavScraper.Configuration;
using Jellyfin.Plugin.JavScraper.Extensions;
using Jellyfin.Plugin.JavScraper.Http;
using Jellyfin.Plugin.JavScraper.Scrapers;
using Jellyfin.Plugin.JavScraper.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        private readonly ILogger _logger;
        private readonly BodyAnalysisService _bodyAnalysisService;
        private readonly IWebProxy _webProxy;
        private readonly Dictionary<string, IScraper> _scrapers;

        public Plugin(
            IApplicationPaths applicationPaths,
            IXmlSerializer xmlSerializer,
            ILoggerFactory loggerFactory,
            BodyAnalysisService bodyAnalysisService,
            IWebProxy webProxy,
            IEnumerable<IScraper> scrapers) : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            _bodyAnalysisService = bodyAnalysisService;
            _webProxy = webProxy;
            _scrapers = scrapers.ToDictionary(scraper => scraper.Name);
            _logger = loggerFactory.CreateLogger<Plugin>();
            _logger.LogInformation("{Name} - Loaded, {Count} scrapers loaded.", Name, _scrapers.Count);
            RefreshByConfig(Configuration);
            ConfigurationChanged = (sender, e) =>
            {
                if (e is not PluginConfiguration config)
                {
                    return;
                }

                _logger.LogInformation("Configuration change, refresh services.");
                RefreshByConfig(config);
            };
        }

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

        private PluginConfiguration RefreshByConfig(PluginConfiguration configuration)
        {
            _logger.LogInformation("call {Method}, {Args}", nameof(RefreshByConfig), $"{nameof(configuration)}={configuration}");
            // update bodyAnalysisService config
            _bodyAnalysisService.RefreshToken(configuration.BaiduBodyAnalysisApiKey, configuration.BaiduBodyAnalysisSecretKey);
            // update scraper config
            var defaultScraperConfigList = _scrapers.Values.Select(scraper => new JavScraperConfigItem() { Name = scraper.Name, Enable = true, Url = scraper.BaseAddress.ToString() }).ToList();
            var scraperConfigList = configuration.ScraperConfigList.Where(scraperConfig => _scrapers.ContainsKey(scraperConfig.Name)).UnionBy(defaultScraperConfigList, config => config.Name).ToList();
            configuration.ScraperConfigList.Clear();
            foreach (var scraperConfig in scraperConfigList)
            {
                if (scraperConfig.Url.IsWebUrl())
                {
                    _scrapers[scraperConfig.Name].BaseAddress = new Uri(scraperConfig.Url);
                }
                else
                {
                    scraperConfig.Url = _scrapers[scraperConfig.Name].BaseAddress.ToString();
                }

                configuration.ScraperConfigList.Add(scraperConfig);
            }

            // update proxy config
            (_webProxy as JavWebProxy)?.Reset(Configuration);
            return configuration;
        }
    }
}
