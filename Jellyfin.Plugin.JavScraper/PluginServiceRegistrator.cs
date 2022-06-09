using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Scrapers;
using Jellyfin.Plugin.JavScraper.Services;
using MediaBrowser.Common.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.JavScraper
{
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection)
        {
            serviceCollection
                .AddSingleton<BodyAnalysisService>()
                .AddSingleton<GfriendsAvatarService>()
                .AddSingleton<TranslationService>()
                .AddSingleton<ImageProxyService>()
                .AddSingleton<ApplicationDbContext>()
                .AddSingleton<AVSOXScraper>()
                .AddSingleton<FC2Scraper>()
                .AddSingleton<Jav123Scraper>()
                .AddSingleton<JavBusScraper>()
                .AddSingleton<JavDBScraper>()
                .AddSingleton<MgsTageScraper>()
                .AddSingleton<R18Scraper>();
        }
    }
}
