using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JavScraper.Services.Model;

namespace Jellyfin.Plugin.JavScraper.Services
{
    /// <summary>
    /// 基础服务
    /// </summary>
    public abstract class BaiduServiceBase : IDisposable
    {
        private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
        private readonly HttpClient _client;
        private bool _isDisposed;

        private BaiduAccessToken? _token;

        protected BaiduServiceBase(string apiKey, string secretKey)
        {
            ApiKey = apiKey;
            SecretKey = secretKey;
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private string ApiKey { get; set; }

        private string SecretKey { get; set; }

        public void RefreshToken(string apiKey, string secretKey)
        {
            ApiKey = apiKey;
            SecretKey = secretKey;
            _token = null;
        }

        private async Task<BaiduAccessToken?> GetAccessTokenAsync(bool force = false)
        {
            await _semaphoreSlim.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!force && _token?.IsValid == true)
                {
                    return _token;
                }

                var dic = new Dictionary<string, string>()
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = ApiKey,
                    ["client_secret"] = SecretKey
                };

                using var content = new FormUrlEncodedContent(dic);
                var resp = await _client.PostAsync("https://aip.baidubce.com/oauth/2.0/token", content).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode == true)
                {
                    var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _token = JsonSerializer.Deserialize<BaiduAccessToken>(json);
                    if (_token != null)
                    {
                        _token.Created = DateTime.Now;
                    }
                }

                return _token;
            }
            finally
            {
                _semaphoreSlim.Release();
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
        public async Task<BaiduApiResult<TResult>?> DoPost<TResult>(string url, object param)
        {
            var token = await GetAccessTokenAsync().ConfigureAwait(false);
            if (token == null)
            {
                return "令牌不正确。";
            }

            var s = url.IndexOf('?', StringComparison.Ordinal) > 0 ? "&" : "?";
            url = $"{url}{s}access_token={token.AccessToken}";
            try
            {
                var json = JsonSerializer.Serialize(param);

                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp = await _client.PostAsync(url, content).ConfigureAwait(false);

                if (resp.IsSuccessStatusCode)
                {
                    json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonSerializer.Deserialize<BaiduApiResult<TResult>>(json);
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
        /// <returns>
        /// An asynchronous result that yields an ApiResult&lt;BaiduFaceApiReault&lt;TResult&gt;&gt;
        /// </returns>
        public async Task<TResult?> DoPostForm<TResult>(string url, Dictionary<string, string> form)
        {
            var token = await GetAccessTokenAsync().ConfigureAwait(false);
            if (token == null)
            {
                return default;
            }

            var s = url.IndexOf('?', StringComparison.Ordinal) > 0 ? "&" : "?";
            url = $"{url}{s}access_token={token.AccessToken}";
            try
            {
                var str = string.Join("&", form.Select(o => $"{o.Key}={WebUtility.UrlEncode(o.Value)}"));
                using var content = new StringContent(str, Encoding.UTF8, "application/x-www-form-urlencoded");
                var resp = await _client.PostAsync(url, content).ConfigureAwait(false);

                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonSerializer.Deserialize<TResult>(json);
                }

                return default;
            }
            catch
            {
                return default;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            if (disposing)
            {
                _semaphoreSlim.Dispose();
                _client.Dispose();
            }

            _isDisposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
