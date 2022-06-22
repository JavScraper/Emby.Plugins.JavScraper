using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jellyfin.Plugin.JavScraper.Extensions;
using Jellyfin.Plugin.JavScraper.Scrapers.Model;
using Jellyfin.Plugin.JavScraper.Services;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JavScraper.Scrapers
{
    public interface IScraper
    {
        /// <summary>
        /// 适配器名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 基础URL
        /// </summary>
        public Uri BaseAddress { get; set; }

        public Task<IReadOnlyList<JavVideoIndex>> Search(string key);

        public Task<JavVideo?> GetJavVideo(JavVideoIndex index);
    }

    /// <summary>
    /// 基础类型
    /// </summary>
    public abstract class AbstractScraper : IScraper
    {
        // ABC-00012 | midv00119
        private static readonly Regex _serial_number_regex = new("^(?<a>[a-z0-9]{3,5})(?<b>[-_ ]+)(?<c>0{1,2})(?<d>[0-9]{3,5})$|^(?<a>[a-z]{3,5})(?<c>0{1,2})(?<d>[0-9]{3,5})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // 7ABC-012
        private static readonly Regex _serial_number_start_with_number_regex = new("^[0-9][a-z]+[-_a-z0-9]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private readonly ILogger _logger;
        private readonly DMMService _dmmService;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="baseUrl">基础URL</param>
        /// <param name="logger">日志记录器</param>
        protected AbstractScraper(string baseUrl, ILogger logger, DMMService dmmService)
        {
            BaseAddress = new Uri(baseUrl);
            _logger = logger;
            _dmmService = dmmService;
        }

        public abstract string Name { get; }

        /// <summary>
        /// 基础URL
        /// </summary>
        public Uri BaseAddress { get; set; }

        /// <summary>
        /// 展开全部的Key
        /// </summary>
        /// <param name="originKey"></param>
        /// <returns></returns>
        private IReadOnlyList<string> GetAllPossibleKeys(string originKey)
        {
            if (string.IsNullOrWhiteSpace(originKey))
            {
                throw new ArgumentException($"{nameof(originKey)} can not be null or space.", nameof(originKey));
            }

            var keyList = new List<string>
            {
                originKey
            };

            // 7ABC-012  --> ABC-012
            if (_serial_number_start_with_number_regex.Match(originKey).Success)
            {
                keyList.Add(originKey[1..]);
            }

            // ABC-00012 --> ABC-012
            var match = _serial_number_regex.Match(originKey);
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

            return morePossibleKeyList.Distinct().OrderBy(key => StringExtensions.CalculateLevenshteinDistance(key, originKey)).ToList();
        }

        /// <summary>
        /// 检查关键字是否符合
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        protected abstract bool IsKeyValid(string key);

        /// <summary>
        /// 获取列表
        /// </summary>
        /// <param name="key">关键字</param>
        /// <returns></returns>
        public virtual async Task<IReadOnlyList<JavVideoIndex>> Search(string key)
        {
            _logger.LogInformation("call {Method}, {Args}", nameof(Search), $"{nameof(key)}={key}");
            try
            {
                if (!IsKeyValid(key))
                {
                    return Array.Empty<JavVideoIndex>();
                }

                var keys = GetAllPossibleKeys(key);
                foreach (var keyWord in keys)
                {
                    var videoIndexList = (await DoSearch(keyWord).ConfigureAwait(false))
                        .Where(index => !string.IsNullOrEmpty(index.Url))
                        .OrderBy(index => StringExtensions.CalculateLevenshteinDistance(index.Num, key)).ToList();
                    if (videoIndexList.Any())
                    {
                        foreach (var index in videoIndexList)
                        {
                            index.Url = CompleteUrlIfNecessary(BaseAddress, index.Url) ?? string.Empty;
                            index.Cover = CompleteUrlIfNecessary(BaseAddress, index.Cover) ?? string.Empty;
                        }

                        return videoIndexList;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fail to call {Method}, {Args}", nameof(Search), $"{nameof(key)}={key}");
            }

            return Array.Empty<JavVideoIndex>();
        }

        protected abstract Task<IReadOnlyCollection<JavVideoIndex>> DoSearch(string key);

        /// <summary>
        /// 获取详情
        /// </summary>
        public virtual async Task<JavVideo?> GetJavVideo(JavVideoIndex index)
        {
            try
            {
                JavVideo? video;
                if (index is JavVideo)
                {
                    video = (JavVideo)index;
                }
                else
                {
                    video = await DoGetJavVideo(index).ConfigureAwait(false);
                }

                if (video == null)
                {
                    return null;
                }

                video.OriginalTitle = video.Title;
                var uri = string.IsNullOrEmpty(index.Url) ? string.IsNullOrEmpty(video.Url) ? BaseAddress : new Uri(video.Url) : new Uri(index.Url);
                video.Cover = CompleteUrlIfNecessary(uri, video.Cover) ?? string.Empty;
                video.Samples = video.Samples.Select(sample => CompleteUrlIfNecessary(uri, sample) ?? string.Empty)
                    .Where(sample => !string.IsNullOrEmpty(sample))
                    .ToList();

                // 去除标题中的番号
                if (!string.IsNullOrWhiteSpace(video.Num) && video.Title.StartsWith(video.Num, StringComparison.OrdinalIgnoreCase))
                {
                    video.Title = video.Title[video.Num.Length..].Trim();
                }

                if (string.IsNullOrWhiteSpace(video.Overview))
                {
                    var overview = await _dmmService.GetOverview(video.Num).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(overview))
                    {
                        video.Overview = video.Title;
                    }
                    else
                    {
                        video.Overview = overview;
                    }
                }

                if (string.IsNullOrWhiteSpace(video.Title))
                {
                    video.Title = video.Num;
                }

                return video;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fail to call {Method}, {Args}", nameof(GetJavVideo), $"{nameof(index)}={index}");
            }

            return null;
        }

        protected abstract Task<JavVideo?> DoGetJavVideo(JavVideoIndex index);

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
    }
}
