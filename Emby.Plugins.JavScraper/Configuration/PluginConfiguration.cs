using Emby.Plugins.JavScraper.Baidu;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace Emby.Plugins.JavScraper.Configuration
{
    /// <summary>
    /// 配置
    /// </summary>
    public class PluginConfiguration
        : BasePluginConfiguration
    {
        /// <summary>
        /// 版本信息
        /// </summary>
        public string Version { get; } = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        /// <summary>
        /// 代理服务器类型
        /// </summary>
        public int ProxyType { get; set; }

        /// <summary>
        /// 启用代理
        /// </summary>
        public bool EnableJsProxy => ProxyType == (int)ProxyTypeEnum.JsProxy && JsProxy.IsWebUrl();

        /// <summary>
        /// JsProxy 代理地址
        /// </summary>
        public string JsProxy { get; set; } = "https://j.javscraper.workers.dev/";

        private const string default_jsProxyBypass = "netcdn.";
        private List<string> _jsProxyBypass;

        /// <summary>
        /// 不走代理的域名
        /// </summary>
        public string JsProxyBypass
        {
            get => _jsProxyBypass?.Any() != true ? default_jsProxyBypass : string.Join(",", _jsProxyBypass);
            set
            {
                _jsProxyBypass = value?.Split(" ,;，；".ToCharArray(), System.StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim())
                    .Distinct().ToList() ?? new List<string>();
            }
        }

        /// <summary>
        /// 是否不走代理
        /// </summary>
        public bool IsBypassed(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return false;
            if (_jsProxyBypass == null)
                JsProxyBypass = default_jsProxyBypass;

            return _jsProxyBypass?.Any(v => host.IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0) == true;
        }

        /// <summary>
        /// 代理服务器：主机
        /// </summary>
        public string ProxyHost { get; set; } = "127.0.0.1";

        /// <summary>
        /// 代理服务器：端口
        /// </summary>
        public int ProxyPort { get; set; } = 7890;

        /// <summary>
        /// 代理服务器：用户名
        /// </summary>
        public string ProxyUserName { get; set; }

        /// <summary>
        /// 代理服务器：密码
        /// </summary>
        public string ProxyPassword { get; set; }

        /// <summary>
        /// 启用 X-FORWARDED-FOR 配置
        /// </summary>
        public bool EnableX_FORWARDED_FOR { get; set; } = true;

        /// <summary>
        /// X-FORWARDED-FOR IP地址
        /// </summary>
        public string X_FORWARDED_FOR { get; set; } = "17.172.224.99";

        private const string default_ignoreGenre = "高畫質,高画质,高清画质,AV女優,AV女优,独占配信,獨佔動畫,DMM獨家,中文字幕,高清,中文,字幕";
        private List<string> _ignoreGenre;

        /// <summary>
        /// 忽略的艺术类型
        /// </summary>
        private Regex regexIgnoreGenre = new Regex(@"^(([\d]{3,4}p)|([\d]{1,2}k)|([\d]{2,3}fps))$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// 忽略的艺术类型
        /// </summary>
        public string IgnoreGenre
        {
            get => _ignoreGenre?.Any() != true ? default_ignoreGenre : string.Join(",", _ignoreGenre);
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    value = default_ignoreGenre;
                _ignoreGenre = value.Split(" ,;，；".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim())
                    .Distinct().ToList();
            }
        }

        /// <summary>
        /// 是不是忽略的艺术类型
        /// </summary>
        public bool IsIgnoreGenre(string genre)
        {
            if (string.IsNullOrWhiteSpace(genre))
                return true;

            if (_ignoreGenre?.Any() != true)
                IgnoreGenre = default_ignoreGenre;
            genre = genre.Trim();
            if (_ignoreGenre?.Any(v => genre.IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0) == true)
                return true;

            return regexIgnoreGenre.IsMatch(genre);
        }

        /// <summary>
        /// 从艺术类型中移除女优的名字
        /// </summary>
        public bool GenreIgnoreActor { get; set; } = true;

        /// <summary>
        /// 从标题结尾处移除女优的名字
        /// </summary>
        public bool TitleIgnoreActor { get; set; } = true;

        /// <summary>
        /// 给 -C 或 -C2 结尾的影片增加“中文字幕”标签
        /// </summary>
        public bool AddChineseSubtitleGenre { get; set; } = true;

        /// <summary>
        /// 标题格式
        /// </summary>
        public string TitleFormat { get; set; } = "%num% %title%";

        /// <summary>
        /// 标题格式-变量为空值时则显示为
        /// </summary>
        public string TitleFormatEmptyValue { get; set; } = "NULL";

        /// <summary>
        /// 刮削器
        /// </summary>
        private List<JavScraperConfigItem> _scrapers;

        /// <summary>
        /// 刮削器
        /// </summary>
        [XmlArrayItem(ElementName = "Scraper")]
        public JavScraperConfigItem[] Scrapers
        {
            get
            {
                var scrapers = Plugin.Instance.Scrapers;
                if (_scrapers?.Any() != true)
                    _scrapers = scrapers.Select(o => new JavScraperConfigItem() { Name = o.Name, Enable = true, Url = o.DefaultBaseUrl }).ToList();
                else
                {
                    //移除重复的
                    _scrapers = _scrapers.GroupBy(o => o.Name).Select(o => o.First()).ToList();

                    var names = scrapers.Select(o => o.Name).ToList();
                    //移除不存在的
                    _scrapers.RemoveAll(o => !names.Contains(o.Name));

                    //如果url不正确则用默认的
                    _scrapers.Where(o => !o.Url.IsWebUrl())
                        .Join(scrapers, o => o.Name, o => o.Name, (o, v) => o.Url = v.DefaultBaseUrl)
                        .ToArray();

                    var exists = _scrapers.Select(o => o.Name).ToList();
                    //新增的
                    var news = scrapers.Where(o => !exists.Contains(o.Name))
                        .Select(o => new JavScraperConfigItem() { Name = o.Name, Enable = true, Url = o.DefaultBaseUrl })
                        .ToList();

                    if (news.Any())
                        _scrapers.AddRange(news);
                }

                return _scrapers?.ToArray();
            }
            set
            {
                _scrapers = value?.Where(o => o != null).GroupBy(o => o.Name).Select(o => o.First()).ToList();
                var scrapers = Plugin.Instance.Scrapers;
                if (_scrapers?.Any() != true)
                    _scrapers = scrapers.Select(o => new JavScraperConfigItem() { Name = o.Name, Enable = true, Url = o.DefaultBaseUrl }).ToList();
                else
                {
                    _scrapers.Join(scrapers, o => o.Name, o => o.Name, (o, v) =>
                    {
                        if (o.Url.IsWebUrl())
                            v.BaseUrl = o.Url;
                        else
                            o.Url = v.DefaultBaseUrl;
                        return true;
                    }).ToArray();
                }
            }
        }

        /// <summary>
        /// 获取启用的刮削器，为空表示全部
        /// </summary>
        public List<JavScraperConfigItem> GetEnableScrapers()
            => _scrapers?.Where(o => o.Enable).ToList();

        #region 百度人体分析

        private bool _EnableBaiduBodyAnalysis = false;

        /// <summary>
        /// 打开百度人体分析
        /// </summary>
        public bool EnableBaiduBodyAnalysis
        {
            get => _EnableBaiduBodyAnalysis && !string.IsNullOrWhiteSpace(BaiduBodyAnalysisApiKey) && !string.IsNullOrWhiteSpace(BaiduBodyAnalysisSecretKey);
            set => _EnableBaiduBodyAnalysis = value;
        }

        /// <summary>
        /// 百度人体分析 ApiKey
        /// </summary>
        public string BaiduBodyAnalysisApiKey { get; set; }

        /// <summary>
        /// 百度人体分析 SecretKey
        /// </summary>
        public string BaiduBodyAnalysisSecretKey { get; set; }

        private BodyAnalysisService bodyAnalysisService;

        /// <summary>
        /// 获取 百度人体分析服务
        /// </summary>
        /// <param name="jsonSerializer"></param>
        /// <returns></returns>
        public BodyAnalysisService GetBodyAnalysisService(IJsonSerializer jsonSerializer)
        {
            if (EnableBaiduBodyAnalysis == false)
                return null;

            if (bodyAnalysisService != null && bodyAnalysisService.ApiKey == BaiduBodyAnalysisApiKey && bodyAnalysisService.SecretKey == BaiduBodyAnalysisSecretKey)
                return bodyAnalysisService;
            BaiduBodyAnalysisApiKey = BaiduBodyAnalysisApiKey.Trim();
            BaiduBodyAnalysisSecretKey = BaiduBodyAnalysisSecretKey.Trim();

            bodyAnalysisService = new BodyAnalysisService(BaiduBodyAnalysisApiKey, BaiduBodyAnalysisSecretKey, jsonSerializer);
            return bodyAnalysisService;
        }

        #endregion 百度人体分析

        #region 百度翻译

        private bool _EnableBaiduFanyi = false;

        /// <summary>
        /// 打开百度翻译
        /// </summary>
        public bool EnableBaiduFanyi
        {
            get => _EnableBaiduFanyi && !string.IsNullOrWhiteSpace(BaiduFanyiApiKey) && !string.IsNullOrWhiteSpace(BaiduFanyiSecretKey);
            set => _EnableBaiduFanyi = value;
        }

        /// <summary>
        /// 百度翻译目标语言：
        /// </summary>
        public string BaiduFanyiLanguage { get; set; } = "zh";

        /// <summary>
        /// 选项
        /// </summary>
        public int BaiduFanyiOptions { get; set; } = (int)(BaiduFanyiOptionsEnum.Name | BaiduFanyiOptionsEnum.Plot);

        /// <summary>
        /// 百度翻译 ApiKey
        /// </summary>
        public string BaiduFanyiApiKey { get; set; }

        /// <summary>
        /// 百度翻译 SecretKey
        /// </summary>
        public string BaiduFanyiSecretKey { get; set; }

        #endregion 百度翻译

        /// <summary>
        /// 剪裁女优头像
        /// </summary>
        public bool EnableCutPersonImage { get; set; } = true;

        #region 类别替换

        /// <summary>
        /// 启用类别替换
        /// </summary>
        public bool EnableGenreReplace { get; set; } = true;

        private List<(string source, string target)> GenreReplaceMaps;

        private string _GenreReplaceMap;

        /// <summary>
        /// 类别替换映射关系
        /// </summary>
        public string GenreReplaceMap
        {
            get => string.IsNullOrWhiteSpace(_GenreReplaceMap) ? DefaultGenreReplaceMap() : _GenreReplaceMap;
            set
            {
                _GenreReplaceMap = value;
                if (string.IsNullOrWhiteSpace(value))
                    _GenreReplaceMap = DefaultGenreReplaceMap();
                GenreReplaceMaps = null;
            }
        }

        /// <summary>
        /// 获取替换表
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public List<(string source, string target)> GetGenreReplaceMaps()
        {
            if (GenreReplaceMaps == null)
            {
                if (string.IsNullOrWhiteSpace(_GenreReplaceMap))
                    GenreReplaceMaps = new List<(string source, string target)>();
                else
                {
                    GenreReplaceMaps = _GenreReplaceMap.Split("\r\n".ToArray(), StringSplitOptions.RemoveEmptyEntries)
                        .Distinct()
                        .Select(o => o.Split(":：".ToArray(), StringSplitOptions.RemoveEmptyEntries))
                        .Where(o => o.Length >= 2)
                        .Select(o => new { key = o[0].Trim(), value = o[1].Trim() })
                        .Where(o => string.IsNullOrWhiteSpace(o.key) == false && string.IsNullOrWhiteSpace(o.value) == false)
                        .GroupBy(o => o.key)
                        .Select(o => (o.Key, o.First().value))
                        .ToList();
                }
            }

            return GenreReplaceMaps;
        }

        private static string DefaultGenreReplaceMap()
            => @"1080p:XXXX
10枚組:10枚组
16時間以上作品:16时间以上作品
4小時以上作品:4小时以上作品
60fps:XXXX
AVOPEN2014スーパーヘビー:AVOPEN2014S级
AVOPEN2014ヘビー級:AVOPEN2014重量级
AVOPEN2014ミドル級:AVOPEN2014中量级
AVOPEN2015SM/ハード部門:AVOPEN2015SM/硬件
AVOPEN2015マニア/フェチ部門:AVOPEN2015狂热者/恋物癖部门
AVOPEN2015女優部門:AVOPEN2015演员部门
AVOPEN2015企画部門:AVOPEN2015企画部门
AVOPEN2015熟女部門:AVOPEN2015熟女部门
AVOPEN2015素人部門:AVOPEN2015素人部门
AVOPEN2015乙女部門:AVOPEN2015少女部
AVOPEN2016ドラマ・ドキュメンタリー部門:AVOPEN2016电视剧纪录部
AVOPEN2016ハード部門:AVOPEN2016ハード部
AVOPEN2016バラエティ部門:AVOPEN2016娱乐部
AVOPEN2016マニア・フェチ部門:AVOPEN2016疯狂恋物科
AVOPEN2016女優部門:AVOPEN2016演员部
AVOPEN2016企画部門:AVOPEN2016企画部
AVOPEN2016人妻・熟女部門:AVOPEN2016人妻・熟女部门
AVOPEN2016素人部門:AVOPEN2016素人部
AVOPEN2016乙女部門:AVOPEN2016少女部
AV女優:AV女优
DMM獨家:DMM独家
DMM專屬:DMM专属
DVD多士爐:DVD多士炉
S級女優:S级女优
Vシネマ:电影放映
アクション:动作
アクメ・オーガズム:绝顶高潮
アスリート:运动员
アニメ:日本动漫
イメージビデオ（男性）:（视频）男性
エマニエル:片商Emanieru熟女塾
エロス:爱的欲望
オタク:御宅族
オナサポ:手淫
お風呂:浴室
お婆ちゃん:外婆
お爺ちゃん:爷爷
キス・接吻:接吻.
コミック雑誌:漫画雑志
サイコ・スリラー:心理惊悚片
サンプル動画:范例影片
スパンキング:打屁股
スマホ専用縦動画:智能手机的垂直视频
スワッピング・夫婦交換:夫妇交换
セクシー:性感美女
セレブ:名流
チアガール:啦啦队女孩
デート:约会
デカチン・巨根:巨根
ノーパン:不穿内裤
ノーブラ:不穿胸罩
ハーレム:后宫
ハイクオリティVR:高品质VR
バック:后卫
ビジネススーツ:商务套装
ビッチ:bitch
ファン感謝・訪問:感恩祭
ブルマ:运动短裤
ヘルス・ソープ:保健香皂
ボーイズラブ:男孩恋爱
ホテル:旅馆
ママ友:妈妈的朋友
ヨガ:瑜伽
ラブコメ:爱情喜剧
愛情旅館:爱情旅馆
白領:白领
白目・失神:白眼失神
伴侶:伴侣
辦公室:办公室
辦公室美女:办公室美女
綁縛:绑缚
薄馬賽克:薄马赛克
鼻フック:鼻钩儿
變態:变态
變態遊戲:变态游戏
變性者:变性人
別墅:别墅
病院・クリニック:医院诊所
播音員:播音员
不穿內褲:不穿内裤
部活・マネージャー:社团经理
殘忍畫面:残忍画面
側位內射:侧位内射
廁所:厕所
插兩根:插两根
插入異物:插入异物
長發:长发
超級女英雄:超级女英雄
車:车
車內:车内
車內性愛:汽车性爱
車掌小姐:车掌小姐
車震:车震
扯破連褲襪:扯破连裤袜
出軌:出轨
廚房:厨房
處男:处男
處女:处女
處女作:处女作
觸手:触手
搭訕:搭讪
打底褲:打底裤
打飛機:打飞机
打手槍:打手枪
打樁機:打桩机
大學生:大学生
大陰蒂:大阴蒂
單體作品:单体作品
蕩婦:荡妇
第一人稱攝影:第一人称摄影
第一視角:第一视角
店員:店员
電車:电车
電動按摩器:电动按摩器
電動陽具:电动阳具
電話:电话
電梯:电梯
電鑽:电钻
調教:调教
丁字褲:丁字裤
動画:动画
動畫人物:动画人物
動漫:动漫
獨立製作:独立制作
獨佔動畫:独佔动画
堵嘴·喜劇:堵嘴·喜剧
短褲:短裤
惡作劇:恶作剧
兒子:儿子
煩惱:烦恼
房間:房间
訪問:访问
糞便:粪便
風格出眾:风格出众
豐滿:丰满
夫婦:夫妇
服務生:服务生
複刻版:复刻版
覆面・マスク:蒙面具
肛門:肛门
高個子:高个子
高畫質:xxx
哥德蘿莉:哥德萝莉
格鬥家:格斗家
各種職業:各种职业
給女性觀眾:女性向
工作人員:工作人员
公共廁所:公共厕所
公交車:公交车
公園:公园
購物:购物
寡婦:寡妇
灌腸:灌肠
國外進口:国外进口
汗だく:汗流浃背
和服・喪服:和服・丧服
黑暗系統:黑暗系统
黑幫成員:黑帮成员
黑髮:黑发
黑人演員:黑人演员
後入:后入
後入內射:后入内射
護士:护士
花癡:花痴
婚禮:婚礼
及膝襪:及膝袜
極小比基尼:极小比基尼
家庭主婦:家庭主妇
假陽具:假阳具
監禁:监禁
檢查:检查
講師:讲师
嬌小:娇小
嬌小的:娇小的
教師:教师
教學:教学
介紹影片:介绍影片
金發:金发
緊縛:紧缚
緊身衣:紧身衣
經典:经典
經期:经期
精液塗抹:精液涂抹
頸鏈:颈链
痙攣:痉挛
局部特寫:局部特写
巨大陽具:巨大阳具
劇情:剧情
捲髮:捲发
開口器:开口器
看護:看护
可愛:可爱
口內射精:口内射精
啦啦隊:啦啦队
蝋燭:蜡烛
蠟燭:蜡烛
濫交:滥交
爛醉如泥的:烂醉如泥的
牢籠:牢笼
老師:老师
連褲襪:连裤袜
連續內射:连续内射
連衣裙:连衣裙
戀愛:恋爱
戀乳癖:恋乳癖
戀腿癖:恋腿癖
戀物癖:恋物癖
獵豔:猎艳
鄰居:邻居
樓梯:楼梯
亂搞:乱搞
亂交:乱交
亂倫:乱伦
輪姦:轮奸
蘿莉:萝莉
蘿莉塔:萝莉塔
裸體襪子:裸体袜子
裸體圍裙:裸体围裙
旅館:旅馆
媽媽:妈妈
罵倒:骂倒
蠻橫嬌羞:蛮横娇羞
貓耳女:猫耳女
貓眼:猫眼
美容師:美容师
門口:门口
迷你係列:迷你系列
秘書:秘书
密會:密会
面接:面试
面試:面试
苗條:苗条
明星臉:明星脸
模特兒:模特
母親:母亲
男の潮吹き:男人高潮
內褲:内裤
內射:内射
內射潮吹:内射潮吹
內射觀察:内射观察
內衣:内衣
逆レイプ:强奸小姨子
逆強姦:逆强奸
年輕:年轻
年輕人妻:年轻人妻
娘・養女:养女
牛仔褲:牛仔裤
農村:农村
女大學生:女大学生
女檢察官:女检察官
女教師:女教师
女僕:女仆
女體盛:女体盛
女同性戀:女同性恋
女王様:女王大人
女醫生:女医生
女傭:女佣
女優ベスト・総集編:演员的总编
女優按摩棒:演员按摩棒
女戰士:女战士
女裝人妖:女装人妖
偶像藝人:偶像艺人
嘔吐:呕吐
拍攝現場:拍摄现场
泡泡襪:泡泡袜
騙奸:骗奸
貧乳・微乳:贫乳・微乳
妻子出軌:妻子出轨
其他戀物癖:其他恋物癖
騎乘內射:骑乘内射
騎乘位:骑乘位
騎在臉上:骑在脸上
企畫:企画
企劃物:企划物
汽車性愛:汽车性爱
強姦:强奸
情侶:情侣
情趣內衣:情趣内衣
親人:亲人
求職:求职
人氣系列:人气系列
日本動漫:日本动漫
日焼け:晒黑
軟体:软体
軟體:软体
潤滑劑:润滑剂
潤滑油:润滑油
賽車女郎:赛车女郎
喪服:丧服
瘙癢:瘙痒
沙發:沙发
曬痕:晒痕
舌頭:舌头
射在頭髮:射在头发
射在外陰:射在外阴
設置項目:设置项目
攝影:摄影
身體意識:身体意识
深膚色:深肤色
繩子:绳子
食糞:食粪
時間停止:时间停止
實拍:实拍
視頻聊天:视频聊天
視訊小姐:视讯小姐
手銬:手铐
首次內射:首次内射
受付嬢:接待小姐
叔母さん:叔母阿姨
束縛:束缚
數位馬賽克:数位马赛克
雙性人:双性人
順從:顺从
私人攝影:私人摄影
絲帶:丝带
送貨上門:送货上门
素顏:素颜
套裝:套装
特典あり（AVベースボール）:特典（AV棒球）
體驗懺悔:体验忏悔
體育服:体育服
舔腳:舔脚
舔陰:舔阴
通姦:通奸
同性戀:同性恋
童顔:童颜
偷窺:偷窥
推薦作品:推荐作品
推銷:推销
襪:袜
外觀相似:外观相似
玩弄肛門:玩弄肛门
晚禮服:晚礼服
網襪:网袜
為智能手機推薦垂直視頻:为智能手机推荐垂直视频
圍裙:围裙
猥褻穿著:猥亵穿着
溫泉:温泉
問卷:问卷
問題:问题
屋頂:屋顶
無碼:无码
無毛:无毛
無套:无套
無套性交:无套性交
西裝:西装
戲劇:戏剧
戲劇x:戏剧
限時降價:限时降价
項圈:项圈
小麥色:小麦色
新娘、年輕妻子:新娘、年轻妻子
性愛:性爱
性伴侶:性伴侣
性別轉型·女性化:性别转型·女性化
性感的x:性感的
性騷擾:性骚扰
胸チラ:露胸
休閒裝:休閒装
羞恥:羞耻
懸掛:悬挂
學生:学生
學生（其他）:学生（其他）
學校:学校
學校泳裝:学校泳装
學校作品:学校作品
鴨嘴:鸭嘴
壓力:压力
亞洲女演員:亚洲女演员
顏面騎乘:颜面骑乘
顏射:颜射
顏射x:颜射
眼鏡:眼镜
眼淚:眼泪
陽具腰帶:阳具腰带
陽台:阳台
藥物:药物
業餘:业余
醫生:医生
醫院:医院
已婚婦女:已婚妇女
異物插入:异物插入
陰道放入食物:阴道放入食物
陰道觀察:阴道观察
陰道擴張:阴道扩张
陰屁:阴屁
淫蕩:淫荡
淫亂:淫乱
淫語:淫语
飲み会・合コン:酒会、联谊会
飲尿:饮尿
泳裝:泳装
遊戲的真人版:游戏真人版
誘惑:诱惑
慾求不滿:慾求不满
原作コラボ:原作协作
遠程操作:远程操作
願望:愿望
約會:约会
孕ませ:孕育
孕婦:孕妇
運動:运动
運動系:运动系
再會:再会
展場女孩:展场女孩
站立姿勢:站立姿势
振動:振动
職員:职员
主動:主动
主婦:主妇
主觀視角:主观视角
注視:注视
子宮頸:子宫颈
字幕:XXXX
做家務:做家务";

        #endregion 类别替换

        #region 演员姓名替换

        /// <summary>
        /// 启用演员姓名替换
        /// </summary>
        public bool EnableActorReplace { get; set; } = false;

        private List<(string source, string target)> ActorReplaceMaps;

        private string _ActorReplaceMap;

        /// <summary>
        /// 演员姓名替换映射关系
        /// </summary>
        public string ActorReplaceMap
        {
            get => string.IsNullOrWhiteSpace(_ActorReplaceMap) ? DefaultActorReplaceMap() : _ActorReplaceMap;
            set
            {
                _ActorReplaceMap = value;
                if (string.IsNullOrWhiteSpace(value))
                    _ActorReplaceMap = DefaultActorReplaceMap();
                ActorReplaceMaps = null;
            }
        }

        /// <summary>
        /// 获取替换表
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public List<(string source, string target)> GetActorReplaceMaps()
        {
            if (ActorReplaceMaps == null)
            {
                if (string.IsNullOrWhiteSpace(_ActorReplaceMap))
                    ActorReplaceMaps = new List<(string source, string target)>();
                else
                {
                    ActorReplaceMaps = _ActorReplaceMap.Split("\r\n".ToArray(), StringSplitOptions.RemoveEmptyEntries)
                        .Distinct()
                        .Select(o => o.Split(":：".ToArray(), StringSplitOptions.RemoveEmptyEntries))
                        .Where(o => o.Length >= 2)
                        .Select(o => new { key = o[0].Trim(), value = o[1].Trim() })
                        .Where(o => string.IsNullOrWhiteSpace(o.key) == false && string.IsNullOrWhiteSpace(o.value) == false)
                        .GroupBy(o => o.key)
                        .Select(o => (o.Key, o.First().value))
                        .ToList();
                }
            }

            return ActorReplaceMaps;
        }

        private static string DefaultActorReplaceMap()
            => @"";

        #endregion 演员姓名替换

        /// <summary>
        /// 文件整理配置
        /// </summary>
        /// <value>The tv options.</value>
        public JavOrganizationOptions JavOrganizationOptions { get; set; } = new JavOrganizationOptions();

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public long ConfigurationVersion { get; set; } = DateTime.Now.Ticks;
    }

    /// <summary>
    /// 代理类型
    /// </summary>
    public enum ProxyTypeEnum
    {
        None = -1,
        JsProxy,
        HTTP,
        HTTPS,
        Socks5
    }

    /// <summary>
    /// 选项
    /// </summary>
    [Flags]
    public enum BaiduFanyiOptionsEnum
    {
        /// <summary>
        /// 标题
        /// </summary>
        Name = 1 << 0,

        /// <summary>
        /// 类别
        /// </summary>
        Genre = 1 << 1,

        /// <summary>
        /// 简介
        /// </summary>
        Plot = 1 << 2,
    }

    /// <summary>
    /// 刮削器配置
    /// </summary>
    public class JavScraperConfigItem
    {
        /// <summary>
        /// 启用
        /// </summary>
        [XmlAttribute]
        public bool Enable { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        [XmlAttribute]
        public string Name { get; set; }

        /// <summary>
        /// 地址
        /// </summary>
        [XmlAttribute]
        public string Url { get; set; }

        public override string ToString()
            => $"{Name} {Enable} {Url}";
    }
}