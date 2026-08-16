#if !__JELLYFIN__
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.GenericEdit;
using MediaBrowser.Model.Plugins.UI.Views;
using System;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper.UI.Views
{
    internal abstract class JavPluginViewBase : IPluginUIView
    {
        protected JavPluginViewBase(string pluginId)
        {
            PluginId = pluginId;
        }

        public event EventHandler<GenericEventArgs<IPluginUIView>> UIViewInfoChanged;

        public virtual string Caption => ContentData?.EditorTitle;

        public virtual string SubCaption => ContentData?.EditorDescription;

        public string PluginId { get; protected set; }

        public IEditableObject ContentData { get; set; }

        public UserDto User { get; set; }

        public string RedirectViewUrl { get; set; }

        public virtual bool IsCommandAllowed(string commandKey)
            => true;

        public virtual Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
            => Task.FromResult<IPluginUIView>(null);

        public virtual Task Cancel()
            => Task.CompletedTask;

        public virtual void OnDialogResult(IPluginUIView dialogView, bool completedOk, object data)
        {
        }

        protected void RaiseUIViewInfoChanged()
            => UIViewInfoChanged?.Invoke(this, new GenericEventArgs<IPluginUIView>(this));
    }
}
#endif
