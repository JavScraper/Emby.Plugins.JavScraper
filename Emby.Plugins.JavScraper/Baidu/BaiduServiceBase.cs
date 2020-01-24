using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Baidu.AI
{
    /// <summary>
    /// 基础服务
    /// </summary>
    public abstract class BaiduServiceBase
    {
        /// <summary>
        /// ApiKey
        /// </summary>
        public string ApiKey { get; }

        /// <summary>
        /// SecretKey
        /// </summary>
        public string SecretKey { get; }

        private BaiduAccessToken token;
        protected HttpClient client;
        private SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
        private readonly IJsonSerializer jsonSerializer;

        /// <summary>
        ///
        /// </summary>
        /// <param name="apiKey"></param>
        /// <param name="secretKey"></param>
        protected BaiduServiceBase(string apiKey, string secretKey, IJsonSerializer jsonSerializer)
        {
            ApiKey = apiKey;
            SecretKey = secretKey;
            this.jsonSerializer = jsonSerializer;
            client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// 获取访问Token
        /// </summary>
        public async Task<BaiduAccessToken> GetAccessTokenAsync(bool force = false)
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                if (force == false && token?.IsValid == true)
                    return token;

                var dic = new Dictionary<string, string>()
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = ApiKey,
                    ["client_secret"] = SecretKey
                };

                var resp = await client.PostAsync("https://aip.baidubce.com/oauth/2.0/token", new FormUrlEncodedContent(dic));
                if (resp.IsSuccessStatusCode == true)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    token = jsonSerializer.DeserializeFromString<BaiduAccessToken>(json);
                    if (token != null)
                        token.created = DateTime.Now;
                }
                return token;
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        /// <summary>
        /// POST 请求
        /// </summary>
        /// <typeparam name="TResult"> Type of the result. </typeparam>
        /// <param name="url"> URL of the resource. </param>
        /// <param name="param"> The parameter. </param>
        /// <returns>
        /// An asynchronous result that yields an ApiResult&lt;BaiduFaceApiReault&lt;TResult&gt;&gt;
        /// </returns>
        public async Task<BaiduApiResult<TResult>> DoPost<TResult>(string url, object param)
        {
            var token = await GetAccessTokenAsync();
            if (token == null)
                return "令牌不正确。";
            var s = url.IndexOf('?') > 0 ? "&" : "?";
            url = $"{url}{s}access_token={token.access_token}";
            try
            {
                var json = jsonSerializer.SerializeToString(param);

                var resp = await client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));

                if (resp.IsSuccessStatusCode)
                {
                    json = await resp.Content.ReadAsStringAsync();
                    return jsonSerializer.DeserializeFromString<BaiduApiResult<TResult>>(json);
                }

                return $"请求出错：{resp.ReasonPhrase}";
            }
            catch (Exception ex)
            {
                return $"请求出错：{ex.Message}";
            }
        }

        /// <summary>
        /// POST 请求
        /// </summary>
        /// <typeparam name="TResult"> Type of the result. </typeparam>
        /// <param name="url"> URL of the resource. </param>
        /// <param name="param"> The parameter. </param>
        /// <returns>
        /// An asynchronous result that yields an ApiResult&lt;BaiduFaceApiReault&lt;TResult&gt;&gt;
        /// </returns>
        public async Task<TResult> DoPostForm<TResult>(string url, Dictionary<string, string> nv)
        {
            var token = await GetAccessTokenAsync();
            if (token == null)
                return default;
            var s = url.IndexOf('?') > 0 ? "&" : "?";
            url = $"{url}{s}access_token={token.access_token}";
            try
            {
                var str = string.Join("&", nv.Select(o => $"{o.Key}={WebUtility.UrlEncode(o.Value)}"));
                var content = new StringContent(str, Encoding.UTF8, "application/x-www-form-urlencoded");
                var resp = await client.PostAsync(url, content);

                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    return jsonSerializer.DeserializeFromString<TResult>(json);
                }

                return default;
            }
            catch
            {
                return default;
            }
        }
    }
}