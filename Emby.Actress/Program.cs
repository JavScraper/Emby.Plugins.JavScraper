using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Emby.Actress
{
    internal class Program
    {
        private static Task Main(string[] args)
        {
            Task ShowError(string _error)
            {
                Console.WriteLine(_error);
                Console.ReadKey();
                return Task.CompletedTask;
            }
            var file = "Config.json";

            Config cfg;
            if (File.Exists(file) == false)
            {
                cfg = new Config();
                File.WriteAllText(file, JsonConvert.SerializeObject(cfg, Formatting.Indented));
                return ShowError($"请使用文本编辑器打开 {file} 文件，配置里面的 Emby url 和 key。");
            }

            var json = File.ReadAllText(file);
            try
            {
                cfg = JsonConvert.DeserializeObject<Config>(json);
            }
            catch
            {
                return ShowError($"配置文件解析失败，请检查格式是否正确。");
            }

            if (string.IsNullOrWhiteSpace(cfg?.api_key))
                return ShowError($"api_key 不能为空。");

            if (string.IsNullOrWhiteSpace(cfg.url))
                return ShowError($"emby 的 url 地址不能为空。");

            try
            {
                new Uri(cfg.url);
            }
            catch
            {
                return ShowError($"{cfg.url} 不是一个有效的 URL 地址。");
            }

            if (string.IsNullOrWhiteSpace(cfg.dir))
                cfg.dir = ".";

            if (Directory.Exists(cfg.dir) == false)
                return ShowError($"请创建名为 【{cfg.dir}】 的文件夹，并把女优头像拷贝到里面。");

            var s = new EmbyActressImportService(cfg);

            return s.StartAsync().ContinueWith(o => Console.ReadKey());
        }
    }
}