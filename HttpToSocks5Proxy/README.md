# HttpToSocks5Proxy [![NuGet](https://img.shields.io/nuget/v/HttpToSocks5Proxy.svg)](https://www.nuget.org/packages/HttpToSocks5Proxy/)

This library allows you to connect over Socks5 proxies when using the .NET `HttpClient`.

It implements the `IWebProxy` interface and can therefore be used with all libraries
that support HTTP/HTTPS proxies.

## Usage with an HttpClient
```c#
using MihaZupan;

var proxy = new HttpToSocks5Proxy("127.0.0.1", 1080);
var handler = new HttpClientHandler { Proxy = proxy };
HttpClient httpClient = new HttpClient(handler, true);

var result = await httpClient.SendAsync(
    new HttpRequestMessage(HttpMethod.Get, "https://httpbin.org/ip"));

Console.WriteLine("HTTPS GET: " + await result.Content.ReadAsStringAsync());
```

## Usage with Telegram.Bot library
The library was originally designed to fight censorship attempts against Telegram.

Using it with the [Telegram Bot Library](https://github.com/TelegramBots/Telegram.Bot)
is therefore a breeze.

```c#
using MihaZupan;

var proxy = new HttpToSocks5Proxy("my-socks-server.com", 1080);

// Or if the proxy server requires credentials (gssapi is not supported):
new HttpToSocks5Proxy("my-socks-server.com", 1080, "username", "password");

// Some proxies limit target connections to a single IP address
// If that is the case you have to resolve hostnames locally
proxy.ResolveHostnamesLocally = true;

TelegramBotClient Bot = new TelegramBotClient("API Token", proxy);
```

## I need more latency
Worry not, you can now chain SOCKS proxies with this library

```c#
var proxy = new HttpToSocks5Proxy(new[] {
    new ProxyInfo("tor-proxy.com", 1080),
    new ProxyInfo("random-socks.com", 1090),
    new ProxyInfo("tor-proxy.com", 1080)
});
```

## Installation

Install as a [NuGet package](https://www.nuget.org/packages/HttpToSocks5Proxy/):

Package manager:

```powershell
Install-Package HttpToSocks5Proxy
```
