#if !__JELLYFIN__
using Emby.Plugins.JavScraper.Configuration;
using Emby.Web.GenericEdit;
using MediaBrowser.Model.Plugins.UI.Views;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper.UI.Views
{
    internal abstract class JavConfigPageView<TUi> : JavPluginViewBase, IPluginPageView
        where TUi : EditableOptionsBase
    {
        protected JavConfigPageView(string pluginId, TUi ui)
            : base(pluginId)
        {
            ContentData = ui;
        }

        protected TUi UI => (TUi)ContentData;

        public bool ShowSave { get; set; } = true;

        public bool ShowBack { get; set; } = false;

        public bool AllowSave { get; set; } = true;

        public bool AllowBack { get; set; } = true;

        public virtual Task<IPluginUIView> OnSaveCommand(string itemId, string commandId, string data)
        {
            var config = Plugin.Instance.Configuration;
            ApplyToConfig(config);
            config.ConfigurationVersion = System.DateTime.Now.Ticks;
            Plugin.Instance.SaveConfiguration();
            return Task.FromResult((IPluginUIView)this);
        }

        protected abstract void ApplyToConfig(PluginConfiguration config);
    }
}
#endif
