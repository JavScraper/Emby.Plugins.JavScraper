using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JavScraper.Execption;
using Jellyfin.Plugin.JavScraper.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Services
{
    /// <summary>
    /// 头像
    /// </summary>
    public sealed class GfriendsAvatarService : IDisposable
    {
        private const string BaseUrl = "https://raw.githubusercontent.com/xinxin8816/gfriends/master/";
        private readonly SemaphoreSlim _locker = new(1, 1);
        private readonly IHttpClientManager _clientFactory;
        private readonly ILogger _logger;

        private FileTreeModel? _tree;
        private DateTime _last = DateTime.Now;

        public GfriendsAvatarService(ILoggerFactory loggerFactory, IHttpClientManager clientFactory)
        {
            _logger = loggerFactory.CreateLogger<GfriendsAvatarService>();
            _clientFactory = clientFactory;
        }

        /// <summary>
        /// 适配器名称
        /// </summary>
        public string Name => "gfriends";

        /// <summary>
        /// 查找女优的头像地址
        /// </summary>
        /// <param name="name">女优姓名</param>
        /// <param name="cancelationToken"></param>
        /// <returns></returns>
        public async Task<string?> FindAvatarAddressAsync(string name, CancellationToken cancelationToken)
        {
            _logger.LogInformation("call {Method}, {Args}", nameof(FindAvatarAddressAsync), $"{nameof(name)}={name}");
            if (_tree == null || (DateTime.Now - _last).TotalHours > 1)
            {
                await _locker.WaitAsync(cancelationToken).ConfigureAwait(false);
                try
                {
                    if (_tree == null || (DateTime.Now - _last).TotalHours > 1)
                    {
                        var json = await _clientFactory.GetClient().GetStringAsync($"{BaseUrl}Filetree.json", cancelationToken).ConfigureAwait(false);
                        _tree = JsonSerializer.Deserialize<FileTreeModel>(json);
                        if (_tree != null)
                        {
                            _last = DateTime.Now;
                            _tree.Content = _tree.Content.OrderBy(o => o.Key).ToDictionary(o => o.Key, o => o.Value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fail to get picture for name={Name}", name);
                }
                finally
                {
                    _locker.Release();
                }
            }

            if (_tree == null)
            {
                throw new DataAccessException($"can not load Filetree.json from {BaseUrl}");
            }

            if (!_tree.Content.Any())
            {
                return null;
            }

            return _tree.Find(name);
        }

        public void Dispose()
        {
            _locker.Dispose();
        }

        /// <summary>
        /// 树模型
        /// </summary>
        internal class FileTreeModel
        {
            /// <summary>
            /// 内容
            /// </summary>
            public Dictionary<string, Dictionary<string, string>> Content { get; set; } = new Dictionary<string, Dictionary<string, string>>();

            /// <summary>
            /// 查找图片
            /// </summary>
            /// <param name="name"></param>
            /// <returns></returns>
            public string? Find(string name)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return null;
                }

                var key = name.Trim();

                foreach (var dd in Content)
                {
                    foreach (var d in dd.Value)
                    {
                        if (d.Key.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                        {
                            return $"{BaseUrl}Content/{dd.Key}/{d.Value}";
                        }
                    }
                }

                return null;
            }
        }
    }
}
