using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Execption;
using Jellyfin.Plugin.JavScraper.Extensions;
using Jellyfin.Plugin.JavScraper.Http;
using Jellyfin.Plugin.JavScraper.Scrapers.Model;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Scrapers
{
    /// <summary>
    /// 基础类型
    /// </summary>
    public abstract class AbstractScraper : IDisposable
    {
        // ABC-00012
        private static readonly Regex _serial_number_regex = new("^(?<a>[a-z0-9]{3,5})(?<b>[-_ ]*)(?<c>0{1,2}[0-9]{3,5})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // 7ABC-012
        private static readonly Regex _serial_number_start_with_number_regex = new("^[0-9][a-z]+[-_a-z0-9]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private readonly ILogger _logger;
        private readonly ApplicationDbContext _applicationDbContext;
        private static readonly NamedAsyncLocker _locker = new();

        private HttpClientFactory _clientFactory;

        private bool _disposedValue;
        private Uri _baseAddress;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="baseUrl">基础URL</param>
        /// <param name="logger">日志记录器</param>
        protected AbstractScraper(string baseUrl, ILogger logger, ApplicationDbContext applicationDbContext)
        {
            DefaultBaseUrl = baseUrl;
            _logger = logger;
            _baseAddress = new Uri(baseUrl);
            _clientFactory = new HttpClientFactory(client => client.BaseAddress = _baseAddress);
            _applicationDbContext = applicationDbContext;
        }

        /// <summary>
        /// 适配器名称
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// 默认的基础URL
        /// </summary>
        public string DefaultBaseUrl { get; }

        /// <summary>
        /// 基础URL
        /// </summary>
        public string BaseAddress
        {
            get => _baseAddress.ToString();
            set
            {
                if (_clientFactory != null && _baseAddress.ToString() == value)
                {
                    return;
                }

                _clientFactory?.Dispose();
                _baseAddress = new Uri(value);
                _clientFactory = new HttpClientFactory(client => client.BaseAddress = _baseAddress);
                _logger.LogInformation("BaseAddress: {}", value);
            }
        }

        /// <summary>
        /// 展开全部的Key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        protected virtual IReadOnlyList<string> GetAllPossibleKeys(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException($"{nameof(key)} can not be null or space.", nameof(key));
            }

            var keyList = new List<string>
            {
                key
            };

            // 7ABC-012  --> ABC-012
            if (_serial_number_start_with_number_regex.Match(key).Success)
            {
                keyList.Add(key[1..]);
            }

            // ABC-00012 --> ABC-012
            var match = _serial_number_regex.Match(key);
            if (match.Success)
            {
                var a = match.Groups["a"].Value;
                var b = match.Groups["b"].Value;
                var c = match.Groups["c"].Value;
                var end = c.TrimStart('0');
                var count = c.Length - end.Length - 1;
                for (var i = 0; i <= count; i++)
                {
                    var em = new string('0', i);
                    keyList.Add($"{a}{em}{end}");
                    keyList.Add($"{a}-{em}{end}");
                    keyList.Add($"{a}_{em}{end}");
                }
            }

            var morePossibleKeyList = new List<string>();
            morePossibleKeyList.AddRange(keyList);
            morePossibleKeyList.AddRange(keyList.Select(x => x.Replace("-", "_", StringComparison.Ordinal)));
            morePossibleKeyList.AddRange(keyList.Select(x => x.Replace("_", "-", StringComparison.Ordinal)));
            morePossibleKeyList.AddRange(keyList.Select(x => x.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal)));

            return morePossibleKeyList.Distinct().ToList();
        }

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="key"></param>
        /// <param name="vedioIndexCollection"></param>
        protected virtual IReadOnlyList<JavVideoIndex> SortIndex(string key, IEnumerable<JavVideoIndex> vedioIndexCollection)
        {
            return vedioIndexCollection.OrderBy(vedioIndex => LevenshteinDistance.Calculate(vedioIndex.Num, key)).ToList();
        }

        /// <summary>
        /// 检查关键字是否符合
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        protected abstract bool CheckKey(string key);

        /// <summary>
        /// 获取列表
        /// </summary>
        /// <param name="key">关键字</param>
        /// <returns></returns>
        public virtual async Task<IReadOnlyList<JavVideoIndex>> Query(string key)
        {
            var vedioIndexList = new List<JavVideoIndex>();
            if (!CheckKey(key))
            {
                return vedioIndexList;
            }

            var keys = GetAllPossibleKeys(key);
            foreach (var k in keys)
            {
                await DoQyery(k).ConfigureAwait(false);
                if (vedioIndexList.Any())
                {
                    foreach (var vedioIndex in vedioIndexList)
                    {
                        vedioIndex.Url = CompleteUrlIfNecessary(_baseAddress, vedioIndex.Url) ?? string.Empty;
                        vedioIndex.Cover = CompleteUrlIfNecessary(_baseAddress, vedioIndex.Cover) ?? string.Empty;
                    }

                    return vedioIndexList;
                }
            }

            return vedioIndexList;
        }

        /// <summary>
        /// 解析列表
        /// </summary>
        /// <returns></returns>
        protected abstract Task<IReadOnlyList<JavVideoIndex>> DoQyery(string key);

        /// <summary>
        /// 解析列表
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        protected abstract IReadOnlyCollection<JavVideoIndex> ParseIndex(HtmlDocument doc);

        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="index">地址</param>
        /// <returns></returns>
        public virtual async Task<JavVideo?> GetJavVedio(JavVideoIndex index)
        {
            var vedio = await GetJavVedio(index.Url).ConfigureAwait(false);
            if (vedio != null)
            {
                vedio.OriginalTitle = vedio.Title;
                try
                {
                    var uri = string.IsNullOrEmpty(index.Url ?? vedio.Url) ? _baseAddress : new Uri(index.Url ?? vedio.Url);
                    vedio.Cover = CompleteUrlIfNecessary(uri, vedio.Cover) ?? string.Empty;
                    vedio.Samples = vedio.Samples.Select(sample => CompleteUrlIfNecessary(uri, sample) ?? string.Empty)
                        .Where(sample => string.IsNullOrEmpty(sample))
                        .ToList();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "fail to initial url for vedio={}", vedio);
                }
            }

            return vedio;
        }

        /// <summary>
        /// 补充完整url
        /// </summary>
        /// <param name="baseUri">基础url</param>
        /// <param name="relativeUri">url或者路径</param>
        /// <returns></returns>
        protected virtual string? CompleteUrlIfNecessary(Uri baseUri, string relativeUri)
        {
            if (string.IsNullOrWhiteSpace(relativeUri))
            {
                return null;
            }

            if (relativeUri.IsWebUrl())
            {
                return relativeUri;
            }

            try
            {
                return new Uri(baseUri, relativeUri).ToString();
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="url">地址</param>
        /// <returns></returns>
        protected abstract Task<JavVideo?> GetJavVedio(string url);

        /// <summary>
        /// 获取 HtmlDocument
        /// </summary>
        /// <param name="requestUri"></param>
        /// <returns></returns>
        protected async Task<HtmlDocument?> GetHtmlDocumentAsync(string requestUri)
        {
            try
            {
                var html = await _clientFactory.GetClient().GetHtmlDocumentAsync(requestUri).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fail to {}", nameof(GetHtmlDocumentAsync));
            }

            return null;
        }

        /// <summary>
        /// 获取 HtmlDocument，通过 Post 方法提交
        /// </summary>
        /// <param name="requestUri"></param>
        /// <returns></returns>
        public virtual Task<HtmlDocument> GetHtmlDocumentByPostAsync(string requestUri, Dictionary<string, string> param)
        {
            using var content = new FormUrlEncodedContent(param);
            return GetHtmlDocumentByPostAsync(requestUri, content);
        }

        /// <summary>
        /// 获取 HtmlDocument，通过 Post 方法提交
        /// </summary>
        /// <param name="requestUri"></param>
        /// <returns></returns>
        public virtual async Task<HtmlDocument> GetHtmlDocumentByPostAsync(string requestUri, HttpContent content)
        {
            try
            {
                var resp = await _clientFactory.GetClient().PostAsync(requestUri, content).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    throw new NetworkException($"fail to {nameof(GetHtmlDocumentByPostAsync)}, response={resp}");
                }

                var html = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                return doc;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "fail to {}", nameof(GetHtmlDocumentByPostAsync));
                throw;
            }
        }

        public virtual async Task<string?> GetDmmPlot(string num)
        {
            const string dmm = "dmm";
            if (string.IsNullOrWhiteSpace(num))
            {
                return null;
            }

            num = num.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).ToLower(CultureInfo.CurrentCulture);
            using (await _locker.WaitAsync(num).ConfigureAwait(false))
            {
                var item = _applicationDbContext.Plots.Find(o => o.Num == num && o.Provider == dmm).FirstOrDefault();
                if (item != null)
                {
                    return item.Info;
                }

                var url = $"https://www.dmm.co.jp/mono/dvd/-/detail/=/cid={num}/";
                var doc = await GetHtmlDocumentAsync(url).ConfigureAwait(false);

                if (doc == null)
                {
                    return null;
                }

                var plot = doc.DocumentNode.SelectSingleNode("//tr/td/div[@class='mg-b20 lh4']/p[@class='mg-b20']")?.InnerText?.Trim();
                if (string.IsNullOrWhiteSpace(plot) == false)
                {
                    var dt = DateTime.Now;
                    item = new Data.Plot()
                    {
                        Created = dt,
                        Modified = dt,
                        Num = num,
                        Info = plot,
                        Provider = dmm,
                        Url = url
                    };
                    _applicationDbContext.Plots.Insert(item);
                }

                return plot;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _clientFactory?.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
