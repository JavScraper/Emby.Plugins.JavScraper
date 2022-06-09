using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.JavScraper.Configuration;
using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Services;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Providers
{
    /*public sealed class CustomServiceProvider : IServerEntryPoint
    {
        private readonly IApplicationPaths _applicationPaths;
        private readonly IApplicationHost _applicationHost;
        private readonly PluginConfiguration _configuration;
        private readonly ILoggerFactory _loggerFactory;

        public CustomServiceProvider(
            IApplicationPaths applicationPaths,
            IApplicationHost applicationHost,
            PluginConfiguration configuration,
            ILoggerFactory loggerFactory)
        {
            _applicationPaths = applicationPaths;
            _applicationHost = applicationHost;
            _configuration = configuration;
            _loggerFactory = loggerFactory;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public Task RunAsync()
        {
            _loggerFactory.CreateLogger<CustomServiceProvider>().LogInformation("{} load", nameof(CustomServiceProvider));
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddSingleton(new BodyAnalysisService(_configuration.BaiduBodyAnalysisApiKey, _configuration.BaiduBodyAnalysisSecretKey))
                .AddSingleton(ApplicationDbContext.Create(_applicationPaths))
                .AddSingleton<GfriendsAvatarService>()
                .AddSingleton<TranslationService>()
                .AddSingleton<ImageProxyService>();
            _applicationHost.Init(serviceCollection);
            return Task.CompletedTask;
        }
    }*/
}
