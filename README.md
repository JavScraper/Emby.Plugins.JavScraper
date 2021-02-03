# Emby.Plugins.JavScraper
Emby/Jellyfin 的一个日本电影刮削器插件，可以从某些网站抓取影片信息。

[https://javscraper.com](https://javscraper.com)

![Jav Scraper Logo](./Emby.Plugins.JavScraper/thumb.png)

关键字：**_Jav_**, **_Scraper_**, **_Jav Scraper_**, **_Emby Plugins_**, **_Jellyfin Plugins_**, **_JavBus_**, **_JavDB_**, **_FC2_**, **_Japanese_**, **_Adult_**, **_Movie_**, **_Metadata_**, **_刮削器_**, **_插件_**, **_日本_**, **_电影_**, **_元数据_**, **_番号_**

# 目录
- [主要原理](#主要原理)
- [支持的采集来源](#支持的采集来源)
- [如何使用](#如何使用)
  * [部署修改版 jsproxy](#部署修改版-jsproxy)
  * [插件安装](#插件安装)
  * [插件更新](#插件更新)
  * [配置](#配置)
  * [使用](#使用)
  * [女优头像](#女优头像)
  * [特别建议](#特别建议)
- [计划新增特性](#计划新增特性)
- [反馈](#反馈)
- [截图](#截图)
  * [效果](#效果)
    + [媒体库](#媒体库)
    + [影片详情](#影片详情)
    + [识别](#识别)
  * [配置](#配置)
    + [Jav Scraper 配置](#jav-scraper-配置)
    + [媒体库配置](#媒体库配置)
    + [女优头像采集](#女优头像采集)

# 主要原理
- 通过在 [CloudFlare Worker](https://workers.cloudflare.com) 上架设的**修改版 [jsproxy](https://github.com/EtherDream/jsproxy)** 作为代理，用于访问几个网站下载元数据和图片。
- 安装到 Emby 的 JavScraper 刮削器插件，根据文件名/文件夹名称找到番号，并下载元数据和图片。

> 目前已经支持 HTTP/HTTPS/SOCKS5 代理方式。

# 支持的采集来源
- [JavBus](https://www.javbus.com/)
- [JavDB](https://javdb.com/)
- [MsgTage](https://www.mgstage.com/)
- [FC2](https://fc2club.com/)
- [AVSOX](https://avsox.host/)
- [Jav123](https://www.jav321.com/)
- [R18](https://www.r18.com/)

# 如何使用

## 部署修改版 jsproxy
具体参见[使用 CloudFlare Worker 免费部署](cf-worker/README.md)
> 默认已经配置了一个代理，多人使用会超过免费的额度，建议自己配置；非中国区或全局穿墙用户，可禁用该代理。

> 目前已经支持 HTTP/HTTPS/SOCKS5 代理方式。

## 插件安装
- [点击这里下载最新的插件文件](https://github.com/JavScraper/Emby.Plugins.JavScraper/releases)，解压出里面的 **JavScraper.dll** 文件，通过ssh等方式拷贝到 Emby 的插件目录
- 常见的插件目录如下：
  - 群晖
    - /volume1/Emby/plugins
    - /var/packages/EmbyServer/var/plugins
    - /volume1/@appdata/EmbyServer/plugins
  - Windows
    - emby\programdata\plugins
- 需要**重启Emby服务**，插件才生效。

## 插件更新
- 打开 **JavScraper** 配置页面的时，会自动检查更新（在页面的最下方）。
- 如果有更新，则点击**立即更新**，并在**重启 Emby Server** 后生效。

## 配置
- 在 **服务器** 配置菜单中找到 **Jav Scraper**，或者 **插件** 菜单中找到 **Jav Scraper** 。
- 配置你自己的 jsproxy 地址 或者 HTTP/HTTPS/SOCKS5 代理。
> 非中国区或全局穿墙用户，可禁用该代理。
- 在**媒体库**中，找到你的**日本电影**的媒体库，并编辑：
    - 媒体库类型必须是**电影** 
    - **显示高级设置**
    - 在 **Movie元数据下载器** 中只 勾选 **JavScraper**
    - 在 **Movie图片获取程序** 中只 勾选 **JavScraper**

## 使用
- _添加新影片后_：在**媒体库**中点 **扫描媒体库文件**；
- _如果需要更新全部元数据_：在**媒体库**中点 **刷新元数据** 
- _如果需要更新某影片元数据_：在**影片**中点 **识别** ，并输入番号查找。

## 女优头像

~~参见 [Emby 女优头像批量导入工具](Emby.Actress/README.md)。~~

已经集成头像采集，可以在 **控制台-高级-计划任务** 中找到 **JavScraper: 采集缺失的女优头像**，并点击右边的三角符号开始启动采集任务。

头像数据源来自 [女友头像仓库](https://github.com/xinxin8816/gfriends)


## 特别建议
- Emby 自动搜索元数据时，会将非根文件夹的名称作为关键字，所以，需要非根文件夹名称中包含番号信息。
- 如果自动搜索元数据失败或者不正确时，请使用 **识别** 功能手动刷新 _单部影片_ 的元数据 或者 修改文件夹、文件名称后再 **扫描媒体库文件**。
- 强烈建议配置**百度的人体分析**接口，这样封面生成会更加准确（_默认等比例截取右边部分作为封面_）。

# 计划新增特性
- [x] 支持某些域名不走代理
- [x] 支持禁用代理
- [x] 支持移除某些标签
- [x] 标签从日文转为中文
- [x] 翻译影片标题、标签、简介
- [x] 刮削器支持排序
- [x] 支持HTTP/HTTPS/SOCKS5代理
- [x] 采集女优头像
- [x] 刮削器支持重新指定网站的域名
- [ ] 文件整理

# 反馈
如果有什么想法，请在[提交反馈](https://github.com/JavScraper/Emby.Plugins.JavScraper/issues)。


# 截图

## 效果

### 媒体库
![Movie Library](https://javscraper.com/Emby.Plugins/Screenshots/Screenshot02.png)

### 影片详情
![Movie Details](https://javscraper.com/Emby.Plugins/Screenshots/Screenshot03.png)

### 识别
![Movie Search](https://javscraper.com/Emby.Plugins/Screenshots/Screenshot04.png)

## 配置
### Jav Scraper 配置 
![Jav Scraper Configuration](https://javscraper.com/Emby.Plugins/Screenshots/Screenshot01.png)

### 媒体库配置

![Library Edit](https://javscraper.com/Emby.Plugins/Screenshots/LibraryEdit01.png)
![Library Edit](https://javscraper.com/Emby.Plugins/Screenshots/LibraryEdit02.png)

### 女优头像采集
![Actress](https://javscraper.com/Emby.Plugins/Screenshots/Actress01.png)