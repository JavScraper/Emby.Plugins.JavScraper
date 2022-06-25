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
                .AddSingleton<DMMService>()
                .AddSingleton<PersonSearchService>()
                .AddSingleton<TranslationService>()
                .AddSingleton<ImageProxyService>()
                .AddSingleton<ApplicationDatabase>()
                .AddSingleton<IScraper, AvsoxScraper>()
                .AddSingleton<IScraper, FC2Scraper>()
                .AddSingleton<IScraper, Jav123Scraper>()
                .AddSingleton<IScraper, JavBusScraper>()
                .AddSingleton<IScraper, JavDbScraper>()
                .AddSingleton<IScraper, MgsTageScraper>()
                .AddSingleton<IScraper, R18Scraper>()
                .AddSingleton<IWebProxy, JavWebProxy>()
                .AddSingleton<IHttpClientManager, HttpClientManager>();
        }
    }
}
