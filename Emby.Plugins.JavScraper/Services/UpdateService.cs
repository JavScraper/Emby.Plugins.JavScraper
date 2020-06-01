using Emby.Plugins.JavScraper.Http;
using Emby.Plugins.JavScraper.Scrapers;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.IO;

#if __JELLYFIN__
using Microsoft.Extensions.Logging;
#else
using MediaBrowser.Model.Logging;
#endif

using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper.Services
{
    /// <summary>
    /// 更新信息
    /// </summary>
    [Route("/emby/Plugins/JavScraper/Update", "GET")]
    public class GetUpdateInfo : IReturn<UpdateInfoData>
    {
        /// <summary>
        /// 是否更新
        /// </summary>
        public bool update { get; set; }
    }

    public class UpdateService : IService
    {
        private readonly IFileSystem fileSystem;
        private readonly IHttpClient httpClient;
        private readonly IZipClient zipClient;
        private readonly IJsonSerializer jsonSerializer;
        private readonly IApplicationPaths appPaths;
        private readonly ILogger logger;
        private static Regex regexVersion = new Regex(@"\d+(?:\.\d+)+");
        private HttpClientEx client;

        public UpdateService(IFileSystem fileSystem, IHttpClient httpClient, IZipClient zipClient, IJsonSerializer jsonSerializer, IApplicationPaths appPaths,
#if __JELLYFIN__
            ILoggerFactory logManager
#else
            ILogManager logManager
#endif
            )
        {
            this.fileSystem = fileSystem;
            this.httpClient = httpClient;
            this.zipClient = zipClient;
            this.jsonSerializer = jsonSerializer;
            this.appPaths = appPaths;
            this.logger = logManager.CreateLogger<UpdateService>();
            client = new HttpClientEx(client => client.DefaultRequestHeaders.UserAgent.TryParseAdd($"JavScraper v{Assembly.GetExecutingAssembly().GetName().Version}"));
        }

        public object Get(GetUpdateInfo request)
        {
            return Task.Run(() => Do(request)).GetAwaiter().GetResult();
        }

        private async Task<UpdateInfoData> Do(GetUpdateInfo request)
        {
            var r = new UpdateInfoData()
            {
                LoadedVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                PendingLoadVersion = GetPendingLoadVersion(),
            };
            try
            {
                var resp = await client.GetAsync("https://api.github.com/repos/JavScraper/Emby.Plugins.JavScraper/releases/latest");

                if (resp.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var data = jsonSerializer.DeserializeFromStream<Rootobject>(await resp.Content.ReadAsStreamAsync());
                    r.UpdateMessage = data.body;

                    string key =
#if __JELLYFIN__
                        "Jellyfin";
#else
                        "Emby.JavScraper";
#endif

                    foreach (var v in data.assets.Where(o => o.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 && o.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
                    {
                        var m = regexVersion.Match(v.name);
                        if (m.Success)
                        {
                            r.LatestVersion = m.ToString();
                            r.LatestUrl = v.browser_download_url;
                            break;
                        }
                    }
                }
                else
                {
                    r.ErrorMessage = "获取新版下失败。";
                    return r;
                }
            }
            catch (Exception ex)
            {
                r.ErrorMessage = ex.Message;
                return r;
            }

            if (request.update == true && r.HasNewVersion)
            {
                try
                {
                    var ms = await client.GetStreamAsync(r.LatestUrl);
                    zipClient.ExtractAllFromZip(ms, appPaths.PluginsPath, true);
                    r.PendingLoadVersion = GetPendingLoadVersion();
                }
                catch (Exception ex)
                {
                    r.ErrorMessage = $"更新失败：{ex.Message}";
                }
            }

            //r.PendingLoadVersion = "1.0.0";
            //r.LoadedVersion = "1.0.0";
            return r;
        }

        private string GetPendingLoadVersion()
        {
            var file = Path.Combine(appPaths.PluginsPath, "JavScraper.dll");
            if (File.Exists(file) == false)
                return null;
            return FileVersionInfo.GetVersionInfo(file)?.FileVersion;
        }
    }

    /// <summary>
    /// 更新信息
    /// </summary>
    public class UpdateInfoData
    {
        /// <summary>
        /// 服务器上的版本
        /// </summary>
        public string LatestVersion { get; set; }

        /// <summary>
        /// 下载地址
        /// </summary>
        public string LatestUrl { get; set; }

        /// <summary>
        /// 加载中的版本
        /// </summary>
        public string LoadedVersion { get; set; }

        /// <summary>
        /// 待加载版本
        /// </summary>
        public string PendingLoadVersion { get; set; }

        /// <summary>
        /// 更新信息
        /// </summary>
        public string UpdateMessage { get; set; }

        /// <summary>
        /// 是否包含新版本
        /// </summary>
        public bool HasNewVersion
        {
            get
            {
                try
                {
                    return string.IsNullOrWhiteSpace(LatestVersion) == false && new Version(LatestVersion) > new Version(PendingLoadVersion ?? "0.0.0.1");
                }
                catch { }

                return false;
            }
        }

        public string ErrorMessage { get; set; }

        public bool Success => string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// 0 错误
        /// 1 新版本
        /// 2 需要重启
        /// 3 最新
        /// </summary>
        public int State
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ErrorMessage) == false)
                    return 0;
                if (HasNewVersion)
                    return 1;
                if (string.IsNullOrWhiteSpace(LatestVersion) == false && new Version(LatestVersion) > new Version(LoadedVersion ?? "0.0.0.1"))
                    return 2;
                return 3;
            }
        }
    }

    public class Rootobject
    {
        public string url { get; set; }
        public string tag_name { get; set; }
        public string name { get; set; }
        public DateTime created_at { get; set; }
        public DateTime published_at { get; set; }
        public Asset[] assets { get; set; }
        public string body { get; set; }
    }

    public class Asset
    {
        public string name { get; set; }
        public object label { get; set; }
        public string content_type { get; set; }
        public string state { get; set; }
        public int size { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime created_at { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime updated_at { get; set; }

        /// <summary>
        /// 下载地址
        /// </summary>
        public string browser_download_url { get; set; }
    }
}