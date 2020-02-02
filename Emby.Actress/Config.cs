namespace Emby.Actress
{
    /// <summary>
    ///
    /// </summary>
    public class Config
    {
        /// <summary>
        /// Emby 站点
        /// </summary>
        public string url { get; set; } = "http://localhost:8096/";

        /// <summary>
        /// Api Key
        /// </summary>
        public string api_key { get; set; } = "";

        /// <summary>
        /// 头像文件夹
        /// </summary>
        public string dir { get; set; } = "女优头像";
    }
}