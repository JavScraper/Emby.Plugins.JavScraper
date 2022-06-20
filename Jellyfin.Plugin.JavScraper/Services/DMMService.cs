using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Jellyfin.Plugin.JavScraper.Data;
using Jellyfin.Plugin.JavScraper.Extensions;
using Jellyfin.Plugin.JavScraper.Http;

namespace Jellyfin.Plugin.JavScraper.Services
{
    public class DMMService
    {
        private static readonly NamedAsyncLocker _locker = new();
        private readonly ApplicationDatabase _applicationDatabase;
        private readonly IHttpClientManager _httpClientManager;

        public DMMService(ApplicationDatabase applicationDatabase, IHttpClientManager httpClientManager)
        {
            _applicationDatabase = applicationDatabase;
            _httpClientManager = httpClientManager;
        }

        public virtual async Task<string?> GetOverview(string num)
        {
            const string DMM = nameof(DMM);
            if (string.IsNullOrWhiteSpace(num))
            {
                return null;
            }

            num = num.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).ToLower(CultureInfo.CurrentCulture);
            using (await _locker.WaitAsync(num).ConfigureAwait(false))
            {
                var item = _applicationDatabase.Overview.Find(o => o.Num == num && o.Provider == DMM).FirstOrDefault();
                if (item != null)
                {
                    return item.Content;
                }

                var url = $"https://www.dmm.co.jp/mono/dvd/-/detail/=/cid={num}/";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Cookie", "age_check_done=1;");
                var doc = await _httpClientManager.GetClient().SendAndReturnHtmlDocumentAsync(request).ConfigureAwait(false);

                if (doc == null)
                {
                    return null;
                }

                var overview = doc.DocumentNode.SelectSingleNode("//tr/td/div[@class='mg-b20 lh4']/p[@class='mg-b20']")?.InnerText?.Trim();
                if (string.IsNullOrWhiteSpace(overview) == false)
                {
                    item = new Overview()
                    {
                        Created = DateTime.Now,
                        Modified = DateTime.Now,
                        Num = num,
                        Content = overview,
                        Provider = DMM,
                        Url = url
                    };
                    _applicationDatabase.Overview.Insert(item);
                }

                return overview;
            }
        }
    }
}
