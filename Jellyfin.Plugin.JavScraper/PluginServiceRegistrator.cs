using System.Net.Http;
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
                .AddSingleton<JavWebProxy>()
                .AddSingleton<AVSOXScraper>()
                .AddSingleton<FC2Scraper>()
                .AddSingleton<Jav123Scraper>()
                .AddSingleton<JavBusScraper>()
                .AddSingleton<JavDBScraper>()
                .AddSingleton<MgsTageScraper>()
                .AddSingleton<R18Scraper>();

            serviceCollection
                .AddHttpClient(Constants.NameClient.DefaultWithProxy, client => client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.130 Safari/537.36"))
                .ConfigurePrimaryHttpMessageHandler(serviceProvider => new ProxyHttpClientHandler(serviceProvider.GetRequiredService<JavWebProxy>()));
        }

        private static void AddNameClientWithProxy(IServiceCollection serviceCollection, string name, string baseAddress) =>
            serviceCollection.AddHttpClient(name, client =>
                {
                    client.BaseAddress = new System.Uri(baseAddress);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.130 Safari/537.36");
                })
                .ConfigurePrimaryHttpMessageHandler(serviceProvider => new ProxyHttpClientHandler(serviceProvider.GetRequiredService<JavWebProxy>()));
    }
}
