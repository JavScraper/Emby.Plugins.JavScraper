使用 CloudFlare Worker 免费部署

# 简介

`CloudFlare Worker` 是 CloudFlare 的边缘计算服务。开发者可通过 JavaScript 对 CDN 进行编程，从而能灵活处理 HTTP 请求。这使得很多任务可在 CDN 上完成，无需自己的服务器参与。


# 部署

首页：[https://workers.cloudflare.com](https://workers.cloudflare.com)

注册，登陆，`Start building`，取一个子域名，`Create a Worker`。

复制 [index.js](index.js) 到左侧代码框，`Save and deploy`。如果正常，右侧应显示首页。

收藏地址框中的 `https://xxxx.子域名.workers.dev`，以后可直接访问。


# 计费

后退到 `overview` 页面可参看使用情况。免费版每天有 10 万次免费请求，对于个人通常足够。


# 特别说明
- 该文件是在 [jsproxy](https://github.com/EtherDream/jsproxy) 的基础进行是修改和简化，在此对原作者[EtherDream](https://github.com/EtherDream)表示感谢。
- 移除了默认的首页，主要是避免用作其他
- 去除请求校验
