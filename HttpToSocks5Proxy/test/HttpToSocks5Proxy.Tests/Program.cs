using System;
using System.Net.Http;
using System.Threading.Tasks;
using MihaZupan;

namespace Socks5Proxy.Tests
{
    class Program
    {
        static async Task Main()
        {
            await Test(false);
            await Test(true);

            Console.ReadLine();
        }

        static async Task Test(bool resolveHostnamesLocally)
        {
            var proxy = new HttpToSocks5Proxy(new[] { new ProxyInfo("127.0.0.1", 9050) });
            var handler = new HttpClientHandler { Proxy = proxy };
            HttpClient httpClient = new HttpClient(handler, true);

            proxy.ResolveHostnamesLocally = resolveHostnamesLocally;

            var result = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://httpbin.org/ip"));
            result.EnsureSuccessStatusCode();

            Console.WriteLine("HTTPS GET: " + await result.Content.ReadAsStringAsync());
        }
    }
}
