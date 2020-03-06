using Emby.Plugins.JavScraper.Baidu;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Emby.Plugins.JavScraper.Configuration
{
    /// <summary>
    /// 配置
    /// </summary>
    public class PluginConfiguration
        : BasePluginConfiguration
    {
        /// <summary>
        /// 版本信息
        /// </summary>
        public string Version { get; } = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        private bool _EnableJsProxy = true;

        /// <summary>
        /// 启用代理
        /// </summary>
        public bool EnableJsProxy { get => _EnableJsProxy && JsProxy.IsWebUrl(); set => _EnableJsProxy = value; }

        /// <summary>
        /// JsProxy 代理地址
        /// </summary>
        public string JsProxy { get; set; } = "https://j.javscraper.workers.dev/";

        private const string default_jsProxyBypass = "netcdn.";
        private List<string> _jsProxyBypass;

        /// <summary>
        /// 不走代理的域名
        /// </summary>
        public string JsProxyBypass
        {
            get => _jsProxyBypass?.Any() != true ? default_jsProxyBypass : string.Join(",", _jsProxyBypass);
            set
            {
                _jsProxyBypass = value?.Split(" ,;，；".ToCharArray(), System.StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim())
                    .Distinct().ToList() ?? new List<string>();
            }
        }

        /// <summary>
        /// 是否不走代理
        /// </summary>
        public bool IsJsProxyBypass(string host)
        {
            if (EnableJsProxy == false)
                return true;

            if (string.IsNullOrWhiteSpace(host))
                return false;
            if (_jsProxyBypass == null)
                JsProxyBypass = default_jsProxyBypass;

            return _jsProxyBypass?.Any(v => host.IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0) == true;
        }

        private const string default_ignoreGenre = "高畫質,高画质,高清画质,AV女優,AV女优,独占配信,獨佔動畫,DMM獨家,中文字幕,高清,中文,字幕";
        private List<string> _ignoreGenre;

        /// <summary>
        /// 忽略的艺术类型
        /// </summary>
        private Regex regexIgnoreGenre = new Regex(@"^(([\d]{3,4}p)|([\d]{1,2}k)|([\d]{2,3}fps))$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// 忽略的艺术类型
        /// </summary>
        public string IgnoreGenre
        {
            get => _ignoreGenre?.Any() != true ? default_ignoreGenre : string.Join(",", _ignoreGenre);
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    value = default_ignoreGenre;
                _ignoreGenre = value.Split(" ,;，；".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim())
                    .Distinct().ToList();
            }
        }

        /// <summary>
        /// 是不是忽略的艺术类型
        /// </summary>
        public bool IsIgnoreGenre(string genre)
        {
            if (string.IsNullOrWhiteSpace(genre))
                return true;

            if (_ignoreGenre?.Any() != true)
                IgnoreGenre = default_ignoreGenre;
            genre = genre.Trim();
            if (_ignoreGenre?.Any(v => genre.IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0) == true)
                return true;

            return regexIgnoreGenre.IsMatch(genre);
        }

        /// <summary>
        /// 从艺术类型中移除女优的名字
        /// </summary>
        public bool GenreIgnoreActor { get; set; } = true;

        /// <summary>
        /// 给 -C 或 -C2 结尾的影片增加“中文字幕”标签
        /// </summary>
        public bool AddChineseSubtitleGenre { get; set; } = true;

        private bool _EnableBaiduBodyAnalysis = false;

        /// <summary>
        /// 打开百度人体分析
        /// </summary>
        public bool EnableBaiduBodyAnalysis
        {
            get => _EnableBaiduBodyAnalysis && !string.IsNullOrWhiteSpace(BaiduBodyAnalysisApiKey) && !string.IsNullOrWhiteSpace(BaiduBodyAnalysisSecretKey);
            set => _EnableBaiduBodyAnalysis = value;
        }

        /// <summary>
        /// 百度人体分析 ApiKey
        /// </summary>
        public string BaiduBodyAnalysisApiKey { get; set; }

        /// <summary>
        /// 百度人体分析 SecretKey
        /// </summary>
        public string BaiduBodyAnalysisSecretKey { get; set; }

        /// <summary>
        /// 构造代理地址
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public string BuildProxyUrl(string url)
            => string.IsNullOrWhiteSpace(url) == false && EnableJsProxy && IsJsProxyBypass(GetHost(url)) == false ? $"{JsProxy.TrimEnd("/")}/http/{url}" : url;

        /// <summary>
        /// 获取域名
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private string GetHost(string url)
        {
            try
            {
                return new Uri(url).Host;
            }
            catch { }

            return url;
        }

        private BodyAnalysisService bodyAnalysisService;

        /// <summary>
        /// 获取 百度人体分析服务
        /// </summary>
        /// <param name="jsonSerializer"></param>
        /// <returns></returns>
        public BodyAnalysisService GetBodyAnalysisService(IJsonSerializer jsonSerializer)
        {
            if (EnableBaiduBodyAnalysis == false)
                return null;

            if (bodyAnalysisService != null && bodyAnalysisService.ApiKey == BaiduBodyAnalysisApiKey && bodyAnalysisService.SecretKey == BaiduBodyAnalysisSecretKey)
                return bodyAnalysisService;
            BaiduBodyAnalysisApiKey = BaiduBodyAnalysisApiKey.Trim();
            BaiduBodyAnalysisSecretKey = BaiduBodyAnalysisSecretKey.Trim();

            bodyAnalysisService = new BodyAnalysisService(BaiduBodyAnalysisApiKey, BaiduBodyAnalysisSecretKey, jsonSerializer);
            return bodyAnalysisService;
        }
    }
}