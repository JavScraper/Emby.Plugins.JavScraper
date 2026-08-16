#if !__JELLYFIN__
using Emby.Plugins.JavScraper.UI.Views;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Model.Plugins.UI.Views;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper.UI
{
    internal class JavMainPageController : JavUiPageControllerBase, IHasTabbedUIPages
    {
        private readonly PluginInfo pluginInfo;
        private readonly List<IPluginUIPageController> tabPages = new List<IPluginUIPageController>();

        public JavMainPageController(PluginInfo pluginInfo)
            : base(pluginInfo.Id)
        {
            this.pluginInfo = pluginInfo;
            PageInfo = new PluginPageInfo
            {
                Name = "Settings",
                DisplayName = "Jav Scrapper",
                EnableInMainMenu = true,
                MenuSection = "server",
                MenuIcon = "search",
                IsMainConfigPage = false
            };

            tabPages.Add(new JavTabPageController(pluginInfo, nameof(ScrapersPageView), "刮削器", () => new ScrapersPageView(pluginInfo)));
            tabPages.Add(new JavTabPageController(pluginInfo, nameof(MetadataPageView), "采集", () => new MetadataPageView(pluginInfo)));
            tabPages.Add(new JavTabPageController(pluginInfo, nameof(BaiduPageView), "百度服务", () => new BaiduPageView(pluginInfo)));
            tabPages.Add(new JavTabPageController(pluginInfo, nameof(ReplaceMapPageView), "替换映射", () => new ReplaceMapPageView(pluginInfo)));
            tabPages.Add(new JavTabPageController(pluginInfo, nameof(OrganizationPageView), "文件整理", () => new OrganizationPageView(pluginInfo)));
        }

        public override PluginPageInfo PageInfo { get; }

        public override Task<IPluginUIView> CreateDefaultPageView()
            => Task.FromResult<IPluginUIView>(new ProxyPageView(pluginInfo));

        public IReadOnlyList<IPluginUIPageController> TabPageControllers => tabPages.AsReadOnly();
    }
}
#endif
