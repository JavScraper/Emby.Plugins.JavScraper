using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Execption;
using Jellyfin.Plugin.JavScraper.Extensions;
using Jellyfin.Plugin.JavScraper.Scrapers.Model;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Scrapers
{
    /// <summary>
    /// 基础类型
    /// </summary>
    public abstract class AbstractScraper
    {
        // ABC-00012 | midv00119
        private static readonly Regex _serial_number_regex = new("^(?<a>[a-z0-9]{3,5})(?<b>[-_ ]+)(?<c>0{1,2})(?<d>[0-9]{3,5})$|^(?<a>[a-z]{3,5})(?<c>0{1,2})(?<d>[0-9]{3,5})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // 7ABC-012
        private static readonly Regex _serial_number_start_with_number_regex = new("^[0-9][a-z]+[-_a-z0-9]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private readonly ILogger _logger;
        private readonly ApplicationDbContext _applicationDbContext;
        private static readonly NamedAsyncLocker _locker = new();

        private IHttpClientFactory _clientFactory;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="baseUrl">基础URL</param>
        /// <param name="logger">日志记录器</param>
        protected AbstractScraper(string baseUrl, ILogger logger, ApplicationDbContext applicationDbContext, IHttpClientFactory httpClientFactory)
        {
            BaseAddress = new Uri(baseUrl);
            _logger = logger;
            _applicationDbContext = applicationDbContext;
            _clientFactory = httpClientFactory;
        }

        /// <summary>
        /// 适配器名称
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// 基础URL
        /// </summary>
        public Uri BaseAddress { get; set; }

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
                var c = match.Groups["c"].Value;
                var d = match.Groups["d"].Value;
                for (var i = 0; i <= c.Length; i++)
                {
                    var em = new string('0', i);
                    keyList.Add($"{a}{em}{d}");
                    keyList.Add($"{a}-{em}{d}");
                    keyList.Add($"{a}_{em}{d}");
                }
            }

            keyList = keyList.Distinct().ToList();

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
            if (!CheckKey(key))
            {
                return Array.Empty<JavVideoIndex>();
            }

            var keys = GetAllPossibleKeys(key);
            foreach (var k in keys)
            {
                var vedioIndexList = await DoQyery(k).ConfigureAwait(false);
                if (vedioIndexList.Any())
                {
                    foreach (var vedioIndex in vedioIndexList)
                    {
                        vedioIndex.Url = CompleteUrlIfNecessary(BaseAddress, vedioIndex.Url) ?? string.Empty;
                        vedioIndex.Cover = CompleteUrlIfNecessary(BaseAddress, vedioIndex.Cover) ?? string.Empty;
                    }

                    return vedioIndexList;
                }
            }

            return Array.Empty<JavVideoIndex>();
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
                    var uri = string.IsNullOrEmpty(index.Url) ? string.IsNullOrEmpty(vedio.Url) ? BaseAddress : new Uri(vedio.Url) : new Uri(index.Url);
                    vedio.Cover = CompleteUrlIfNecessary(uri, vedio.Cover) ?? string.Empty;
                    vedio.Samples = vedio.Samples.Select(sample => CompleteUrlIfNecessary(uri, sample) ?? string.Empty)
                        .Where(sample => string.IsNullOrEmpty(sample))
                        .ToList();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "fail to initial url for vedio={Vedio}", vedio);
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
                return await _clientFactory.CreateClient(Constants.NameClient.DefaultWithProxy).GetHtmlDocumentAsync(new Uri(BaseAddress, requestUri)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fail to {Method}", nameof(GetHtmlDocumentAsync));
            }

            return null;
        }

        /// <summary>
        /// 获取 HtmlDocument，通过 Post 方法提交
        /// </summary>
        /// <param name="requestUri"></param>
        /// <returns></returns>
        public virtual async Task<HtmlDocument> GetHtmlDocumentByPostAsync(string requestUri, Dictionary<string, string> param)
        {
            using var content = new FormUrlEncodedContent(param);
            return await GetHtmlDocumentByPostAsync(requestUri, content).ConfigureAwait(true);
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
                var resp = await _clientFactory.CreateClient(Constants.NameClient.DefaultWithProxy).PostAsync(new Uri(BaseAddress, requestUri), content).ConfigureAwait(false);
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
                _logger.LogError(ex, "fail to {Method}", nameof(GetHtmlDocumentByPostAsync));
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
    }
}
