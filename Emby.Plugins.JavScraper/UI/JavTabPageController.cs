#if !__JELLYFIN__
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;
using System;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper.UI
{
    internal class JavTabPageController : JavUiPageControllerBase
    {
        private readonly Func<IPluginUIView> factoryFunc;

        public JavTabPageController(PluginInfo pluginInfo, string name, string displayName, Func<IPluginUIView> factoryFunc)
            : base(pluginInfo.Id)
        {
            this.factoryFunc = factoryFunc;
            PageInfo = new PluginPageInfo
            {
                Name = name,
                DisplayName = displayName
            };
        }

        public override PluginPageInfo PageInfo { get; }

        public override Task<IPluginUIView> CreateDefaultPageView()
            => Task.FromResult(factoryFunc());
    }
}
#endif
