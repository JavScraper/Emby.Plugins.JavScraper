# Emby.Actress
Emby 女优头像批量导入工具

>已经集成头像采集，可以在 **控制台-高级-计划任务** 中找到 **JavScraper: 采集缺失的女优头像**，并点击右边的三角符号开始启动采集任务。

# 用途
通过调用 Emby 的接口，将网友收集好的女优头像批量导入到 Emby 中

# 下载
[点击这里下载](https://github.com/JavScraper/Emby.Plugins.JavScraper/releases/tag/v1.22.27.1109)
## 文件说明
### [Emby.Actress@20200202-Windows.zip](https://github.com/JavScraper/Emby.Plugins.JavScraper/releases/download/v1.22.27.1109/Emby.Actress@20200202-Windows.zip)
- 依赖 [.NET Framework 4.6.1](https://support.microsoft.com/zh-cn/help/3102436/the-net-framework-4-6-1-offline-installer-for-windows) 
- Windows 用户首选，Windows 7、8、10 都自带有运行库了。
### [Emby.Actress@20200202-dotnet_core.zip](https://github.com/JavScraper/Emby.Plugins.JavScraper/releases/download/v1.22.27.1109/Emby.Actress@20200202-dotnet_core.zip)
- 依赖 [.NET Core 3.1 Runtime](https://dotnet.microsoft.com/download/dotnet-core/current/runtime)
- Linux/MAC OSX/Windows 可用。

# 使用

## 女优头像获取
### 自己收集
自己去网上收集，并保存为 `女优名.jpg` 的名称。支持 `.jpg`、`jpeg`、'png' 三种图片格式。

### 使用网友已经收集好的
点这里 [女优头像](https://github.com/junerain123/javsdt/releases/tag/%E5%A5%B3%E4%BC%98%E5%A4%B4%E5%83%8F) 下载名为 [actors.zip](https://github.com/junerain123/javsdt/releases/download/%E5%A5%B3%E4%BC%98%E5%A4%B4%E5%83%8F/actors.zip)
 的压缩包。

## 配置
### Config.json 文件说明
```json
{
  "url": "http://localhost:8096/",
  "api_key": "c976d594ee1f466da82bfd434f481234",
  "dir": "女优头像"
}
```
- **url** 你自己 Emby 服务器的地址
- **api_key** 点击 右上角的齿轮 **管理 Emby 服务器** - **高级** - **API 密钥** 中添加。
- **dir** 女优头像所在文件夹。

## 执行
- Windows 下载双击执行 `Emby.Actress.exe` 即可。
- Linux/Mac 下执行 `dotnet Emby.Actress.dll`

# 如何一次性导入全部女优
- 导入程序执行的时候会生成一个 `<dir>.nfo` 的文件，比如：`女优头像.nfo`。
- 在 `媒体库` 中的 `媒体资料储存方式` 勾选 `Nfo`。
- 在`媒体库所在的文件夹`中，拷贝一个文件体积较小的电影，并重命名与之前的`nfo文件同名`，比如`女优头像.mkv`
- 把`女优头像.nfo`拷贝到和`女优头像.mkv`同一个文件夹下。
- 点击媒体库中的`扫描媒体库文件`，等待在媒体库中找到`女优头像`这部电影，点进去，会发现下面一堆演员。
- 重新运行上面的程序，就可以一次性导入全部的女优了。