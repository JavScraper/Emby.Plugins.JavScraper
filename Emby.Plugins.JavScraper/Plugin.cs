using Emby.Plugins.JavScraper.Configuration;
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
using Emby.Plugins.JavScraper.Data;
using Emby.Plugins.JavScraper.Services;
using MediaBrowser.Model.IO;
using Emby.Plugins.JavScraper.Scrapers;
using System.Reflection;
using System.Net.Http;
using System.Linq;
using System.Collections.ObjectModel;

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
        /// 翻译服务
        /// </summary>
        public TranslationService TranslationService { get; }

        /// <summary>
        /// 图片代理服务
        /// </summary>
        public ImageProxyService ImageProxyService { get; }

        /// <summary>
        /// 全部的刮削器
        /// </summary>
        public ReadOnlyCollection<AbstractScraper> Scrapers { get; }

        /// <summary>
        /// COPY TO /volume1/@appstore/EmbyServer/releases/4.3.1.0/plugins
        /// </summary>
        /// <param name="applicationPaths"></param>
        /// <param name="xmlSerializer"></param>
        /// <param name="logger"></param>
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, IJsonSerializer jsonSerializer, IFileSystem fileSystem,
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

            db = ApplicationDbContext.Create(applicationPaths);
            TranslationService = new TranslationService(jsonSerializer, logManager.CreateLogger<TranslationService>());
            ImageProxyService = new ImageProxyService(jsonSerializer, logManager.CreateLogger<ImageProxyService>(), fileSystem, applicationPaths);
            Scrapers = GetScrapers(null, logManager).AsReadOnly();
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

        /// <summary>
        /// 枚举系统中全部的刮削器
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="logManager"></param>
        /// <returns></returns>
        private List<AbstractScraper> GetScrapers(HttpClientHandler handler = null,
#if __JELLYFIN__
            ILoggerFactory logManager
#else
            ILogManager logManager
#endif
            = null)
        {
            var ls = new List<AbstractScraper>();
            var base_type = typeof(AbstractScraper);
            var types = Assembly.GetExecutingAssembly().GetTypes()
                 .Where(o => base_type != o && base_type.IsAssignableFrom(o))
                 .ToList();
            var p1 = typeof(HttpClientHandler);
            var p2 = typeof(ILogger);
            foreach (var type in types)
            {
                foreach (var c in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).OrderByDescending(o => o.GetParameters()?.Count() ?? 0))
                {
                    var ps = c.GetParameters();
                    var param = new List<object>();
                    var notfound = false;
                    foreach (var p in ps)
                    {
                        if (p.ParameterType == p1 || p1.IsAssignableFrom(p.ParameterType))
                            param.Add(handler);
                        else if (p.ParameterType == p2)
                            param.Add(logManager?.CreateLogger(type));
                        else
                        {
                            notfound = true;
                            break;
                        }
                    }
                    if (notfound)
                        continue;
                    try
                    {
                        var cc = Activator.CreateInstance(type, param.ToArray()) as AbstractScraper;
                        ls.Add(cc);
                    }
                    catch { }
                }
            }
            return ls;
        }
    }
}