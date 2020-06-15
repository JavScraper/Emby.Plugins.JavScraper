using Emby.Plugins.JavScraper.Http;
using Emby.Plugins.JavScraper.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;

#if __JELLYFIN__
using Microsoft.Extensions.Logging;
#else
using MediaBrowser.Model.Logging;
#endif

using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper
{
    public class JavPersonProvider : IRemoteMetadataProvider<Person, PersonLookupInfo>, IHasOrder
    {
        private readonly HttpClientEx client;
        private readonly ILogger _logger;
        private readonly IProviderManager providerManager;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IApplicationPaths _appPaths;
        private const string base_url = "https://raw.githubusercontent.com/xinxin8816/gfriends/master/";

        public int Order => 4;

        public string Name => Plugin.NAME + "-Actress";

        public ImageProxyService ImageProxyService { get; }

        public JavPersonProvider(
#if __JELLYFIN__
            ILoggerFactory logManager
#else
            ILogManager logManager
#endif
            , IProviderManager providerManager, IJsonSerializer jsonSerializer, IFileSystem fileSystem, IApplicationPaths appPaths)
        {
            _logger = logManager.CreateLogger<JavPersonProvider>();
            this.providerManager = providerManager;
            _jsonSerializer = jsonSerializer;
            _appPaths = appPaths;
            ImageProxyService = new ImageProxyService(jsonSerializer, logManager.CreateLogger<ImageProxyService>(), fileSystem, appPaths);
            client = new HttpClientEx(client => client.BaseAddress = new Uri(base_url));
        }

        private FileTreeModel tree;
        private DateTime last = DateTime.Now.AddDays(-1);
        private readonly SemaphoreSlim locker = new SemaphoreSlim(1, 1);

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(PersonLookupInfo info, CancellationToken cancelationToken)
        {
            var list = new List<RemoteSearchResult>();
            if (string.IsNullOrWhiteSpace(info.Name))
                return list;

            await locker.WaitAsync(cancelationToken);
            try
            {
                if (tree == null || (DateTime.Now - last).TotalHours > 1)
                {
                    var json = await client.GetStringAsync("Filetree.json");
                    tree = _jsonSerializer.DeserializeFromString<FileTreeModel>(json);
                    last = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
            }
            finally
            {
                locker.Release();
            }

            if (tree?.Content?.Any() != true)
                return list;

            var url = tree.Find(info.Name);

            if (string.IsNullOrWhiteSpace(url))
                return list;
            url = $"{base_url}{url}";
            var result = new RemoteSearchResult
            {
                Name = info.Name,
                ImageUrl = $"/emby/Plugins/JavScraper/Image?url={url}",
                SearchProviderName = Name,
            };
            result.ProviderIds[Name] = url;
            list.Add(result);

            return list;
        }

        public async Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo info, CancellationToken cancellationToken)
        {
            var metadataResult = new MetadataResult<Person>();
            string url = null;

            _logger?.Info($"{nameof(GetMetadata)} info:{_jsonSerializer.SerializeToString(info)}");

            if ((url = info.GetProviderId(Name)) == null)
            {
                var res = await GetSearchResults(info, cancellationToken).ConfigureAwait(false);
                if (res.Count() == 0 || (url = res.FirstOrDefault().GetProviderId(Name)) == null)
                {
                    _logger?.Info($"{nameof(GetMetadata)} name:{info.Name} not found 0.");
                    return metadataResult;
                }
            }

            if (url == null)
            {
                _logger?.Info($"{nameof(GetMetadata)} name:{info.Name} not found 1.");
                return metadataResult;
            }

            metadataResult.HasMetadata = true;

            metadataResult.Item = new Person()
            {
                ProviderIds = new Dictionary<string, string> { { Name, url } },
                Overview = "\u200B"
            };

            return metadataResult;
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancelToken)
        {
            //  /emby/Plugins/JavScraper/Image?url=
            if (url.IndexOf("Plugins/JavScraper/Image", StringComparison.OrdinalIgnoreCase) >= 0) //本地的链接
            {
                var start = url.IndexOf('=');
                url = url.Substring(start + 1);
                if (url.Contains("://") == false)
                    url = WebUtility.UrlDecode(url);
            }
            _logger?.Info($"{nameof(GetImageResponse)} {url}");
            return ImageProxyService.GetImageResponse(url, ImageType.Backdrop, cancelToken);
        }

        /// <summary>
        /// 树模型
        /// </summary>
        public class FileTreeModel
        {
            /// <summary>
            /// 内容
            /// </summary>
            public Dictionary<string, Dictionary<string, string>> Content { get; set; }

            /// <summary>
            /// 查找图片
            /// </summary>
            /// <param name="name"></param>
            /// <returns></returns>
            public string Find(string name)
            {
                if (string.IsNullOrWhiteSpace(name))
                    return null;

                var key = $"{name.Trim()}.";

                foreach (var dd in Content)
                {
                    foreach (var d in dd.Value)
                    {
                        if (d.Key.StartsWith(key))
                            return $"Content/{dd.Key}/{d.Value}";
                    }
                }

                return null;
            }
        }
    }
}