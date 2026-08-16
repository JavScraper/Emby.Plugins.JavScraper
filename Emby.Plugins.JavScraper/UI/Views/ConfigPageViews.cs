#if !__JELLYFIN__
using Emby.Plugins.JavScraper.Configuration;
using Emby.Plugins.JavScraper.UI.ViewModels;
using MediaBrowser.Model.Plugins;
using System;
using System.Linq;

namespace Emby.Plugins.JavScraper.UI.Views
{
    internal class ProxyPageView : JavConfigPageView<ProxyUI>
    {
        public ProxyPageView(PluginInfo pluginInfo)
            : base(pluginInfo.Id, new ProxyUI(Plugin.Instance.Configuration))
        {
        }

        protected override void ApplyToConfig(PluginConfiguration config)
        {
            config.ProxyType = UI.ProxyType;
            config.JsProxy = UI.JsProxy;
            config.JsProxyBypass = UI.JsProxyBypass;
            config.ProxyHost = UI.ProxyHost;
            config.ProxyPort = UI.ProxyPort;
            config.ProxyUserName = UI.ProxyUserName;
            config.ProxyPassword = UI.ProxyPassword;
            config.EnableX_FORWARDED_FOR = UI.EnableX_FORWARDED_FOR;
            config.X_FORWARDED_FOR = UI.X_FORWARDED_FOR;
        }
    }

    internal class ScrapersPageView : JavConfigPageView<ScrapersUI>
    {
        public ScrapersPageView(PluginInfo pluginInfo)
            : base(pluginInfo.Id, new ScrapersUI(Plugin.Instance.Configuration))
        {
        }

        protected override void ApplyToConfig(PluginConfiguration config)
        {
            var items = UI.ScraperItems?
                .OfType<ScraperEditorItem>()
                .Where(o => !string.IsNullOrWhiteSpace(o.Name))
                .Select(o => o.ToConfig())
                .ToArray();

            if (items?.Length > 0)
                config.Scrapers = items;
        }
    }

    internal class MetadataPageView : JavConfigPageView<MetadataUI>
    {
        public MetadataPageView(PluginInfo pluginInfo)
            : base(pluginInfo.Id, new MetadataUI(Plugin.Instance.Configuration))
        {
        }

        protected override void ApplyToConfig(PluginConfiguration config)
        {
            config.IgnoreGenre = UI.IgnoreGenre;
            config.GenreIgnoreActor = UI.GenreIgnoreActor;
            config.TitleIgnoreActor = UI.TitleIgnoreActor;
            config.AddChineseSubtitleGenre = UI.AddChineseSubtitleGenre;
            config.TitleFormat = UI.TitleFormat;
            config.TitleFormatEmptyValue = UI.TitleFormatEmptyValue;
            config.EnableCutPersonImage = UI.EnableCutPersonImage;
        }
    }

    internal class BaiduPageView : JavConfigPageView<BaiduUI>
    {
        public BaiduPageView(PluginInfo pluginInfo)
            : base(pluginInfo.Id, new BaiduUI(Plugin.Instance.Configuration))
        {
        }

        protected override void ApplyToConfig(PluginConfiguration config)
        {
            config.EnableBaiduBodyAnalysis = UI.EnableBaiduBodyAnalysis;
            config.BaiduBodyAnalysisApiKey = UI.BaiduBodyAnalysisApiKey;
            config.BaiduBodyAnalysisSecretKey = UI.BaiduBodyAnalysisSecretKey;
            config.EnableBaiduFanyi = UI.EnableBaiduFanyi;
            config.BaiduFanyiLanguage = UI.BaiduFanyiLanguage;
            config.BaiduFanyiApiKey = UI.BaiduFanyiApiKey;
            config.BaiduFanyiSecretKey = UI.BaiduFanyiSecretKey;

            var options = 0;
            if (UI.TranslateTitle)
                options |= (int)BaiduFanyiOptionsEnum.Name;
            if (UI.TranslateGenre)
                options |= (int)BaiduFanyiOptionsEnum.Genre;
            if (UI.TranslatePlot)
                options |= (int)BaiduFanyiOptionsEnum.Plot;
            config.BaiduFanyiOptions = options;
        }
    }

    internal class ReplaceMapPageView : JavConfigPageView<ReplaceMapUI>
    {
        public ReplaceMapPageView(PluginInfo pluginInfo)
            : base(pluginInfo.Id, new ReplaceMapUI(Plugin.Instance.Configuration))
        {
        }

        protected override void ApplyToConfig(PluginConfiguration config)
        {
            config.EnableGenreReplace = UI.EnableGenreReplace;
            config.GenreReplaceMap = UI.GenreReplaceMap;
            config.EnableActorReplace = UI.EnableActorReplace;
            config.ActorReplaceMap = UI.ActorReplaceMap;
        }
    }

    internal class OrganizationPageView : JavConfigPageView<OrganizationUI>
    {
        public OrganizationPageView(PluginInfo pluginInfo)
            : base(pluginInfo.Id, new OrganizationUI(Plugin.Instance.Configuration))
        {
        }

        protected override void ApplyToConfig(PluginConfiguration config)
        {
            var options = config.JavOrganizationOptions ?? new JavOrganizationOptions();
            options.WatchLocations = string.IsNullOrWhiteSpace(UI.WatchLocation)
                ? Array.Empty<string>()
                : new[] { UI.WatchLocation.Trim() };
            options.TargetLocation = UI.TargetLocation;
            options.MinFileSizeMb = UI.MinFileSizeMb;
            options.MovieFolderPattern = UI.MovieFolderPattern;
            options.MoviePattern = UI.MoviePattern;
            options.AddChineseSubtitleSuffix = UI.AddChineseSubtitleSuffix;
            options.CopyOriginalFile = UI.CopyOriginalFile;
            options.OverwriteExistingFiles = UI.OverwriteExistingFiles;
            options.LeftOverFileExtensionsToDelete = (UI.LeftOverFileExtensionsText ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(o => o.Trim())
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            options.DeleteEmptyFolders = UI.DeleteEmptyFolders;
            options.ExtendedClean = UI.ExtendedClean;
            config.JavOrganizationOptions = options;
        }
    }
}
#endif
