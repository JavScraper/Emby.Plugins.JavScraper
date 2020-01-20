# Emby.Plugins.JavScraper
Emby 的一个日本电影刮削器插件，可以从某些网站抓取影片信息。

# 主要原理
- 通过在 [CloudFlare Worker](https://workers.cloudflare.com) 上架设的**修改版 [jsproxy](https://github.com/EtherDream/jsproxy)** 作为代理，用于访问几个网站下载元数据和图片。
- 安装到 Emby 的 JavScraper 刮削器插件，根据文件名找到番号，并下载元数据和图片。

# 如何使用

## 部署修改版 jsproxy
具体参见[使用 CloudFlare Worker 免费部署](cf-worker/README.md)

## 补丁安装
- 下载最新版本插件文件或者下载源码编译，通过ssh等方式拷贝到 Emby 的插件目录
- 常见的插件目录如下：
  - 群晖
    > /volume1/@appstore/EmbyServer/releases/4.3.1.0/plugins
  - Windows
    > emby\programdata\plugins

## 配置
- 在**插件** 菜单中找到 **JavScraper**，点击进去，配置你自己的 jsproxy 地址。
- 在**媒体库**中，找到你的日本电影的媒体库，并编辑：
    - 打开高级设置
    - 在 **Movie元数据下载器** 中只 勾选 **JavScraper**
    - 在 **Movie 图片获取程序** 中只 勾选 **JavScraper**

## 使用
- 点 **刷新元数据** 或者 在 **识别** 中输入番号查找。