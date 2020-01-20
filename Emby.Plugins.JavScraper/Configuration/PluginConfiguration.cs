using MediaBrowser.Model.Plugins;

namespace Emby.Plugins.JavScraper.Configuration
{
    /// <summary>
    /// 配置
    /// </summary>
    public class PluginConfiguration
        : BasePluginConfiguration
    {
        /// <summary>
        /// JsProxy 代理地址
        /// </summary>
        public string JsProxy { get; set; } = "https://j.javscraper.workers.dev/";

        /// <summary>
        /// 是否包含 JsProxy
        /// </summary>
        public bool HasJsProxy
            => JsProxy.IsWebUrl();

        /// <summary>
        /// 构造代理地址
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public string BuildProxyUrl(string url)
            => string.IsNullOrWhiteSpace(url) == false && HasJsProxy ? $"{JsProxy.TrimEnd("/")}/http/{url}" : url;
    }
}