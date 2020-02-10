using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Emby.Actress
{
    public class EmbyActressImportService
    {
        private HttpClient client;
        private Config cfg;

        public EmbyActressImportService(Config cfg)
        {
            this.cfg = cfg;
            client = new HttpClient();
            client.BaseAddress = new Uri($"{cfg.url?.TrimEnd('/')}/emby/");
        }

        private string Get(IEnumerable<string> ls)
        {
            var sb = new StringBuilder();

            sb.AppendLine($@"<?xml version=""1.0"" encoding=""utf-8"" standalone=""yes""?>
<movie>
  <dateadded>{DateTime.Now:yyyy-MM-dd HH:mm}</dateadded>
  <title>全部女优</title>");

            foreach (var a in ls)
            {
                sb.AppendLine($@"  <actor>
    <name>{a}</name>
    <type>Actor</type>
  </actor>");
            }

            sb.AppendLine("</movie>");

            return sb.ToString();
        }

        internal async Task StartAsync()
        {
            var dir = cfg.dir;
            var files = Directory.GetFiles(dir, "*.jpg", SearchOption.AllDirectories).Union(Directory.GetFiles(dir, "*.jpeg", SearchOption.AllDirectories))
                  .Union(Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories))
                  .Select(o => new { name = Path.GetFileNameWithoutExtension(o), file = o }).ToList();

            if (files.Count == 0)
            {
                Console.WriteLine($"{Path.GetFileName(dir)} 中没有女优头像。");
                return;
            }
            Console.WriteLine($"{Path.GetFileName(dir)} 中找到 {files.Count} 个女优头像。");

            var nfo_name = $"{Path.GetFileNameWithoutExtension(dir)}.nfo";
            var nfo_txt = Get(files.Select(o => o.name));

            try
            {
                File.WriteAllText(nfo_name, nfo_txt);
                Console.WriteLine($"保存 {nfo_name} 文件成功，如何使用请参阅 https://github.com/JavScraper/Emby.Plugins.JavScraper/Emby.Actress");
            }
            catch
            {
                Console.WriteLine($"保存 {nfo_name} 文件失败。");
            }

            var pesions = await GetPesionsAsync();
            if (pesions == null)
            {
                Console.WriteLine("查询演职员信息失败，请检查 url 和 api_key 配置是否正确。");
                return;
            }
            var total = pesions.Count();
            pesions = pesions.Where(o => o.ImageTags?.ContainsKey("Primary") != true)
                .ToList();

            Console.WriteLine($"在 Emby 中找到 {total} 个演职员，其中  {pesions.Count} 个没有头像。");

            if (pesions.Count == 0)
            {
                Console.WriteLine("没有演职员需要更新头像。");
                return;
            }

            var all = pesions.Join(files, o => o.Name, o => o.name, (o, v) => new { persion = o, file = v }).ToList();

            if (all.Count == 0)
            {
                Console.WriteLine("没有匹配的演职员需要更新头像。");
                await SaveMissing();
                return;
            }

            int i = 0;
            int c = all.Count;
            foreach (var a in all)
            {
                var imageContent = new StringContent(Convert.ToBase64String(File.ReadAllBytes(a.file.file)));
                if (a.file.file.EndsWith("png", StringComparison.OrdinalIgnoreCase))
                    imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
                else
                    imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");

                var action = $"Items/{a.persion.Id}/Images/Primary";
                i++;
                Console.WriteLine($"{i}/{c} {i * 1.0 / c:p} {a.persion.Name}");
                try
                {
                    var r = await DoPost<dynamic>(action, imageContent);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{a.persion.Name} 更新失败：{ex.Message}");
                }
            }

            await SaveMissing();
        }

        private async Task SaveMissing()
        {
            var dir = cfg.dir;

            var pesions = await GetPesionsAsync();
            if (pesions == null)
            {
                Console.WriteLine("重新获取演员失败。");
                return;
            }

            pesions = pesions.Where(o => o.ImageTags?.ContainsKey("Primary") != true)
                .ToList();

            if (pesions.Count == 0)
            {
                Console.WriteLine("全部演职员已经有头像了。");
                return;
            }
            Console.WriteLine($"在 Emby 中找到 {pesions.Count} 个演职员没有头像。");


            var missing_name = $"{Path.GetFileNameWithoutExtension(dir)}.Missing.txt";

            try
            {
                File.WriteAllText(missing_name, string.Join(Environment.NewLine, pesions.Select(o => o.Name)));
                Console.WriteLine($"保存 {missing_name} 文件成功，以上演职员缺少头像。");
            }
            catch
            {
                Console.WriteLine($"保存 {missing_name} 文件失败。");
            }
        }

        public async Task<List<PesionData>> GetPesionsAsync()
        {
            var ll = await DoGet<EmbyListReault<PesionData>>("Persons");

            return ll?.Items;
        }

        /// <summary>
        /// Post 操作
        /// </summary>
        /// <param name="action">操作</param>
        /// <param name="model"></param>
        /// <returns></returns>
        internal Task<TResult> DoPostAsJson<TResult>(string action, object model)
            where TResult : new()
        {
            var sp = action?.IndexOf("?") >= 0 ? "&" : "?";
            action = $"{action}{sp}api_key={cfg.api_key}";

            var task = client.PostAsJsonAsync(action, model);
            return DoProcess<TResult>(task);
        }

        /// <summary>
        /// Post 操作
        /// </summary>
        /// <param name="action">操作</param>
        /// <param name="model"></param>
        /// <returns></returns>
        internal Task<TResult> DoPost<TResult>(string action, HttpContent httpContent)
            where TResult : new()
        {
            var sp = action?.IndexOf("?") >= 0 ? "&" : "?";
            action = $"{action}{sp}api_key={cfg.api_key}";

            var task = client.PostAsync(action, httpContent);
            return DoProcess<TResult>(task);
        }

        /// <summary>
        /// Get 操作
        /// </summary>
        /// <param name="action">操作</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        internal Task<TResult> DoGet<TResult>(string action, Dictionary<string, string> param = null)
            where TResult : new()
        {
            if (param == null)
                param = new Dictionary<string, string>();
            param["api_key"] = cfg.api_key;

            var p = string.Join("&", param.Select(o => $"{o.Key}={HttpUtility.UrlEncode(o.Value ?? string.Empty)}"));
            var sp = action?.IndexOf("?") >= 0 ? "&" : "?";
            action = $"{action}{sp}{p}";

            var task = client.GetAsync(action);
            return DoProcess<TResult>(task);
        }

        /// <summary>
        /// HTTP 请求处理
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="index">The index.</param>
        /// <param name="task">The task.</param>
        /// <returns></returns>
        internal async Task<TResult> DoProcess<TResult>(Task<HttpResponseMessage> task)
            where TResult : new()
        {
            string json = null;
            try
            {
                var r = await task;
                json = await r.Content.ReadAsStringAsync();

                if (r.IsSuccessStatusCode == false)
                {
                    return default;
                }
                return JsonConvert.DeserializeObject<TResult>(json);
            }
            catch
            {
                return default;
            }
        }
    }

    public static class HttpClientExtensions
    {
        public static async Task<HttpResponseMessage> PostAsJsonAsync<TModel>(this HttpClient client, string requestUrl, TModel model)
        {
            var json = JsonConvert.SerializeObject(model);
            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");
            return await client.PostAsync(requestUrl, stringContent);
        }
    }

    public class EmbyListReault<TData>
    {
        public List<TData> Items { get; set; }
        public int TotalRecordCount { get; set; }
    }

    public class PesionData
    {
        public string Name { get; set; }
        public string ServerId { get; set; }
        public string Id { get; set; }
        public string Type { get; set; }
        public Dictionary<string, string> ImageTags { get; set; }
        public object[] BackdropImageTags { get; set; }

        public override string ToString()
            => Name;
    }
}