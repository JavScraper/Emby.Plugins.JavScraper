#if !__JELLYFIN__
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Model.Plugins.UI.Views;
using System.Threading;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper.UI
{
    internal abstract class JavUiPageControllerBase : IPluginUIPageController
    {
        protected JavUiPageControllerBase(string pluginId)
        {
            PluginId = pluginId;
        }

        public abstract PluginPageInfo PageInfo { get; }

        public string PluginId { get; }

        public virtual Task Initialize(CancellationToken token)
            => Task.CompletedTask;

        public abstract Task<IPluginUIView> CreateDefaultPageView();
    }
}
#endif
