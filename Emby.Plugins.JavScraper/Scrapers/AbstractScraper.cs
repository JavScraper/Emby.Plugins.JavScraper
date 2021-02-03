using Emby.Plugins.JavScraper.Http;
using HtmlAgilityPack;

#if __JELLYFIN__
using Microsoft.Extensions.Logging;
#else
using MediaBrowser.Model.Logging;
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper.Scrapers
{
    /// <summary>
    /// 基础类型
    /// </summary>
    public abstract class AbstractScraper
    {
        protected HttpClientEx client;
        protected ILogger log;
        private static NamedLockerAsync locker = new NamedLockerAsync();

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
        private string base_url = null;

        /// <summary>
        /// 基础URL
        /// </summary>
        public string BaseUrl
        {
            get => base_url;
            set
            {
                if (value.IsWebUrl() != true)
                    return;
                if (base_url == value && client != null)
                    return;
                base_url = value;
                client = new HttpClientEx(client => client.BaseAddress = new Uri(base_url));
                log?.Info($"BaseUrl: {base_url}");
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="base_url">基础URL</param>
        /// <param name="log">日志记录器</param>
        public AbstractScraper(string base_url, ILogger log)
        {
            this.log = log;
            DefaultBaseUrl = base_url;
            BaseUrl = base_url;
        }

        //ABC-00012 --> ABC-012
        protected static Regex regexKey = new Regex("^(?<a>[a-z0-9]{3,5})(?<b>[-_ ]*)(?<c>0{1,2}[0-9]{3,5})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        //7ABC-012  --> ABC-012
        protected static Regex regexKey2 = new Regex("^[0-9][a-z]+[-_a-z0-9]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// 展开全部的Key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        protected virtual List<string> GetAllKeys(string key)
        {
            var ls = new List<string>();

            var m = regexKey2.Match(key);
            if (m.Success)
                ls.Add(key.Substring(1));

            ls.Add(key);

            m = regexKey.Match(key);
            if (m.Success)
            {
                var a = m.Groups["a"].Value;
                var b = m.Groups["b"].Value;
                var c = m.Groups["c"].Value;
                var end = c.TrimStart('0');
                var count = c.Length - end.Length - 1;
                for (int i = 0; i <= count; i++)
                {
                    var em = i > 0 ? new string('0', i) : string.Empty;
                    ls.Add($"{a}{em}{end}");
                    ls.Add($"{a}-{em}{end}");
                    ls.Add($"{a}_{em}{end}");
                }
            }

            if (key.IndexOf('-') > 0)
                ls.Add(key.Replace("-", "_"));
            if (key.IndexOf('_') > 0)
                ls.Add(key.Replace("_", "-"));

            if (ls.Count > 1)
                ls.Add(key.Replace("-", "").Replace("_", ""));

            return ls;
        }

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="key"></param>
        /// <param name="ls"></param>
        protected virtual void SortIndex(string key, List<JavVideoIndex> ls)
        {
            if (ls?.Any() != true)
                return;

            //返回的多个结果中，第一个未必是最匹配的，需要手工匹配下
            if (ls.Count > 1 && string.Compare(ls[0].Num, key, true) != 0) //多个结果，且第一个不一样
            {
                var m = ls.FirstOrDefault(o => string.Compare(o.Num, key, true) == 0)
                    ?? ls.Select(o => new { m = o, v = LevenshteinDistance.Calculate(o.Num.ToUpper(), key.ToUpper()) }).OrderBy(o => o.v).FirstOrDefault().m;

                ls.Remove(m);
                ls.Insert(0, m);
            }
        }

        /// <summary>
        /// 检查关键字是否符合
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public abstract bool CheckKey(string key);

        /// <summary>
        /// 获取列表
        /// </summary>
        /// <param name="key">关键字</param>
        /// <returns></returns>
        public virtual async Task<List<JavVideoIndex>> Query(string key)
        {
            var ls = new List<JavVideoIndex>();
            if (CheckKey(key) == false)
                return ls;
            var keys = GetAllKeys(key);
            foreach (var k in keys)
            {
                await DoQyery(ls, k);
                if (ls.Any())
                    return ls;
            }
            return ls;
        }

        /// <summary>
        /// 解析列表
        /// </summary>
        /// <param name="ls"></param>
        /// <param name="doc"></param>
        /// <returns></returns>
        protected abstract Task<List<JavVideoIndex>> DoQyery(List<JavVideoIndex> ls, string key);

        /// <summary>
        /// 解析列表
        /// </summary>
        /// <param name="ls"></param>
        /// <param name="doc"></param>
        /// <returns></returns>
        protected abstract List<JavVideoIndex> ParseIndex(List<JavVideoIndex> ls, HtmlDocument doc);

        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="index">地址</param>
        /// <returns></returns>
        public virtual async Task<JavVideo> Get(JavVideoIndex index)
        {
            var r = await Get(index?.Url);
            if (r != null)
                r.OriginalTitle = r.Title;
            return r;
        }

        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="url">地址</param>
        /// <returns></returns>
        public abstract Task<JavVideo> Get(string url);

        /// <summary>
        /// 获取 HtmlDocument
        /// </summary>
        /// <param name="requestUri"></param>
        /// <returns></returns>
        public virtual async Task<HtmlDocument> GetHtmlDocumentAsync(string requestUri)
        {
            try
            {
                var html = await client.GetStringAsync(requestUri);
                if (string.IsNullOrWhiteSpace(html) == false)
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);
                    return doc;
                }
            }
            catch (Exception ex)
            {
                log?.Error($"{ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 获取 HtmlDocument，通过 Post 方法提交
        /// </summary>
        /// <param name="requestUri"></param>
        /// <returns></returns>
        public virtual Task<HtmlDocument> GetHtmlDocumentByPostAsync(string requestUri, Dictionary<string, string> param)
            => GetHtmlDocumentByPostAsync(requestUri, new FormUrlEncodedContent(param));

        /// <summary>
        /// 获取 HtmlDocument，通过 Post 方法提交
        /// </summary>
        /// <param name="requestUri"></param>
        /// <returns></returns>
        public virtual async Task<HtmlDocument> GetHtmlDocumentByPostAsync(string requestUri, HttpContent content)
        {
            try
            {
                var resp = await client.PostAsync(requestUri, content);
                if (resp.IsSuccessStatusCode == false)
                {
                    var eee = await resp.Content.ReadAsStringAsync();
                    return null;
                }

                var html = await resp.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(html) == false)
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);
                    return doc;
                }
            }
            catch (Exception ex)
            {
                log?.Error($"{ex.Message}");
            }

            return null;
        }

        public virtual async Task<string> GetDmmPlot(string num)
        {
            const string dmm = "dmm";
            if (string.IsNullOrWhiteSpace(num))
                return null;

            num = num.Replace("-", "").Replace("_", "").ToLower();
            using (await locker.LockAsync(num))
            {
                var item = Plugin.Instance.db.Plots.Find(o => o.num == num && o.provider == dmm).FirstOrDefault();
                if (item != null)
                    return item.plot;

                var url = $"https://www.dmm.co.jp/mono/dvd/-/detail/=/cid={num}/";
                var doc = await GetHtmlDocumentAsync(url);

                if (doc == null)
                    return null;

                var plot = doc.DocumentNode.SelectSingleNode("//tr/td/div[@class='mg-b20 lh4']/p[@class='mg-b20']")?.InnerText?.Trim();
                if (string.IsNullOrWhiteSpace(plot) == false)
                {
                    var dt = DateTime.Now;
                    item = new Data.Plot()
                    {
                        created = dt,
                        modified = dt,
                        num = num,
                        plot = plot,
                        provider = dmm,
                        url = url
                    };
                    Plugin.Instance.db.Plots.Insert(item);
                }

                return plot;
            }
        }
    }
}