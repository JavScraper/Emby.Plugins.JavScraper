using System.Net;
using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Http;
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
                .AddSingleton<AbstractScraper, AVSOXScraper>()
                .AddSingleton<AbstractScraper, FC2Scraper>()
                .AddSingleton<AbstractScraper, Jav123Scraper>()
                .AddSingleton<AbstractScraper, JavBusScraper>()
                .AddSingleton<AbstractScraper, JavDBScraper>()
                .AddSingleton<AbstractScraper, MgsTageScraper>()
                .AddSingleton<AbstractScraper, R18Scraper>()
                .AddSingleton<IWebProxy, JavWebProxy>()
                .AddSingleton<ICustomHttpClientFactory, HttpClientFactory>();
        }
    }
}
