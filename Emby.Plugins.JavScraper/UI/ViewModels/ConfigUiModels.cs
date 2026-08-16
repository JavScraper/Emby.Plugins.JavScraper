#if !__JELLYFIN__
using Emby.Plugins.JavScraper.Configuration;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using MediaBrowser.Model.Attributes;
using System;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace Emby.Plugins.JavScraper.UI.ViewModels
{
    internal class ProxyUI : EditableOptionsBase
    {
        public override string EditorTitle => "代理服务器";
        public override string EditorDescription => "配置网络代理和 X-FORWARDED-FOR 请求头。";

        [Browsable(false)]
        public EditorSelectOption[] ProxyTypeOptions => new[]
        {
            new EditorSelectOption("-1", "不使用代理", true, null, null, null, null),
            new EditorSelectOption("0", "JsProxy", true, null, null, null, null),
            new EditorSelectOption("1", "HTTP", true, null, null, null, null),
            new EditorSelectOption("2", "HTTPS", true, null, null, null, null),
            new EditorSelectOption("3", "Socks5", true, null, null, null, null)
        };

        public ProxyUI()
        {
        }

        public ProxyUI(PluginConfiguration config)
        {
            ProxyType = config.ProxyType;
            JsProxy = config.JsProxy;
            JsProxyBypass = config.JsProxyBypass;
            ProxyHost = config.ProxyHost;
            ProxyPort = config.ProxyPort;
            ProxyUserName = config.ProxyUserName;
            ProxyPassword = config.ProxyPassword;
            EnableX_FORWARDED_FOR = config.EnableX_FORWARDED_FOR;
            X_FORWARDED_FOR = config.X_FORWARDED_FOR;
        }

        [DisplayName("代理服务器类型")]
        [SelectItemsSource(nameof(ProxyTypeOptions))]
        public int ProxyType { get; set; }

        [DisplayName("JsProxy 代理地址")]
        public string JsProxy { get; set; }

        [DisplayName("JsProxy 跳过域名")]
        [Description("多个域名可用空格、逗号或分号分隔。")]
        [EditMultiline(2)]
        public string JsProxyBypass { get; set; }

        [DisplayName("代理主机")]
        public string ProxyHost { get; set; }

        [DisplayName("代理端口")]
        [MinValue(1)]
        [MaxValue(65535)]
        public int ProxyPort { get; set; }

        [DisplayName("代理用户名")]
        public string ProxyUserName { get; set; }

        [DisplayName("代理密码")]
        [IsPassword]
        public string ProxyPassword { get; set; }

        [DisplayName("启用 X-FORWARDED-FOR")]
        public bool EnableX_FORWARDED_FOR { get; set; }

        [DisplayName("X-FORWARDED-FOR IP 地址")]
        [Description("用于伪造来源 IP 地址以突破部分站点的区域限制。")]
        public string X_FORWARDED_FOR { get; set; }
    }

    internal class ScrapersUI : EditableOptionsBase
    {
        public override string EditorTitle => "刮削器";
        public override string EditorDescription => "启用或调整各刮削器的基础地址。";

        public ScrapersUI()
        {
        }

        public ScrapersUI(PluginConfiguration config)
        {
            ScraperItems = new EditableObjectCollection(
                (config.Scrapers ?? Array.Empty<JavScraperConfigItem>())
                    .Select(ScraperEditorItem.FromConfig)
            );
        }

        [DisplayName("刮削器")]
        public EditableObjectCollection ScraperItems { get; set; } = new EditableObjectCollection();
    }

    internal class ScraperEditorItem : EditableOptionsBase
    {
        public override string EditorTitle => string.IsNullOrWhiteSpace(Name) ? "刮削器" : Name;

        [DisplayName("启用")]
        public bool Enable { get; set; }

        [DisplayName("名称")]
        [ReadOnly(true)]
        public string Name { get; set; }

        [DisplayName("地址")]
        public string Url { get; set; }

        public static ScraperEditorItem FromConfig(JavScraperConfigItem item)
            => new ScraperEditorItem
            {
                Name = item?.Name,
                Enable = item?.Enable ?? true,
                Url = item?.Url
            };

        public JavScraperConfigItem ToConfig()
            => new JavScraperConfigItem
            {
                Name = Name?.Trim(),
                Enable = Enable,
                Url = Url?.Trim()
            };
    }

    internal class MetadataUI : EditableOptionsBase
    {
        public override string EditorTitle => "采集与标题";
        public override string EditorDescription => "配置标签处理、标题格式和头像裁剪。";

        public MetadataUI()
        {
        }

        public MetadataUI(PluginConfiguration config)
        {
            IgnoreGenre = config.IgnoreGenre;
            GenreIgnoreActor = config.GenreIgnoreActor;
            TitleIgnoreActor = config.TitleIgnoreActor;
            AddChineseSubtitleGenre = config.AddChineseSubtitleGenre;
            TitleFormat = config.TitleFormat;
            TitleFormatEmptyValue = config.TitleFormatEmptyValue;
            EnableCutPersonImage = config.EnableCutPersonImage;
        }

        [DisplayName("忽略类别")]
        [Description("多个类别可用空格、逗号或分号分隔。")]
        [EditMultiline(3)]
        public string IgnoreGenre { get; set; }

        [DisplayName("类别中移除演员名")]
        public bool GenreIgnoreActor { get; set; }

        [DisplayName("标题结尾移除演员名")]
        public bool TitleIgnoreActor { get; set; }

        [DisplayName("添加中文字幕类别")]
        public bool AddChineseSubtitleGenre { get; set; }

        [DisplayName("标题格式")]
        [Description("支持 %num%、%title%、%title_original%、%actor%、%actor_first%、%set%、%director%、%date%、%year%、%month%、%studio%、%maker%、%genre:A?B:C% 等变量。")]
        public string TitleFormat { get; set; }

        [DisplayName("标题变量为空时显示")]
        public string TitleFormatEmptyValue { get; set; }

        [DisplayName("裁剪演员头像")]
        [Description("按比例裁剪演员头像，避免 Emby 中显示变形。")]
        public bool EnableCutPersonImage { get; set; }
    }

    internal class BaiduUI : EditableOptionsBase
    {
        public override string EditorTitle => "百度服务";
        public override string EditorDescription => "配置百度人体分析和百度翻译。";

        [Browsable(false)]
        public EditorSelectOption[] LanguageOptions => new[]
        {
            new EditorSelectOption("zh", "中文", true, null, null, null, null),
            new EditorSelectOption("cht", "繁体中文", true, null, null, null, null),
            new EditorSelectOption("en", "英文", true, null, null, null, null)
        };

        public BaiduUI()
        {
        }

        public BaiduUI(PluginConfiguration config)
        {
            EnableBaiduBodyAnalysis = config.EnableBaiduBodyAnalysis;
            BaiduBodyAnalysisApiKey = config.BaiduBodyAnalysisApiKey;
            BaiduBodyAnalysisSecretKey = config.BaiduBodyAnalysisSecretKey;
            EnableBaiduFanyi = config.EnableBaiduFanyi;
            BaiduFanyiLanguage = config.BaiduFanyiLanguage;
            BaiduFanyiApiKey = config.BaiduFanyiApiKey;
            BaiduFanyiSecretKey = config.BaiduFanyiSecretKey;
            TranslateTitle = (config.BaiduFanyiOptions & (int)BaiduFanyiOptionsEnum.Name) != 0;
            TranslateGenre = (config.BaiduFanyiOptions & (int)BaiduFanyiOptionsEnum.Genre) != 0;
            TranslatePlot = (config.BaiduFanyiOptions & (int)BaiduFanyiOptionsEnum.Plot) != 0;
        }

        [DisplayName("启用百度人体分析")]
        public bool EnableBaiduBodyAnalysis { get; set; }

        [DisplayName("百度人体分析 ApiKey")]
        public string BaiduBodyAnalysisApiKey { get; set; }

        [DisplayName("百度人体分析 SecretKey")]
        [IsPassword]
        public string BaiduBodyAnalysisSecretKey { get; set; }

        [DisplayName("启用百度翻译")]
        public bool EnableBaiduFanyi { get; set; }

        [DisplayName("百度翻译目标语言")]
        [SelectItemsSource(nameof(LanguageOptions))]
        public string BaiduFanyiLanguage { get; set; }

        [DisplayName("翻译标题")]
        public bool TranslateTitle { get; set; }

        [DisplayName("翻译类别")]
        [Description("旧页面标注为不推荐。")]
        public bool TranslateGenre { get; set; }

        [DisplayName("翻译简介")]
        public bool TranslatePlot { get; set; }

        [DisplayName("百度翻译 ApiKey")]
        public string BaiduFanyiApiKey { get; set; }

        [DisplayName("百度翻译 SecretKey")]
        [IsPassword]
        public string BaiduFanyiSecretKey { get; set; }
    }

    internal class ReplaceMapUI : EditableOptionsBase
    {
        public override string EditorTitle => "替换映射";
        public override string EditorDescription => "类别替换优先级高于百度翻译类别项。目标值为 XXXX 表示移除。";

        public ReplaceMapUI()
        {
        }

        public ReplaceMapUI(PluginConfiguration config)
        {
            EnableGenreReplace = config.EnableGenreReplace;
            GenreReplaceMap = config.GenreReplaceMap;
            EnableActorReplace = config.EnableActorReplace;
            ActorReplaceMap = config.ActorReplaceMap;
        }

        [DisplayName("启用类别替换")]
        public bool EnableGenreReplace { get; set; }

        [DisplayName("类别替换映射")]
        [Description("每行一条，格式：原类别:目标类别。目标类别为 XXXX 时表示移除该类别。")]
        [EditMultiline(14)]
        public string GenreReplaceMap { get; set; }

        [DisplayName("启用演员姓名替换")]
        public bool EnableActorReplace { get; set; }

        [DisplayName("演员姓名替换映射")]
        [Description("每行一条，格式：原姓名:目标姓名。目标姓名为 XXXX 时表示移除该演员。")]
        [EditMultiline(8)]
        public string ActorReplaceMap { get; set; }
    }

    internal class OrganizationUI : EditableOptionsBase
    {
        public override string EditorTitle => "文件整理";
        public override string EditorDescription => "整理功能仍属于实验能力，请确认源目录和目标目录都已加入媒体库。";

        [Browsable(false)]
        public EditorSelectOption[] ChineseSubtitleSuffixOptions => new[]
        {
            new EditorSelectOption("0", "不添加", true, null, null, null, null),
            new EditorSelectOption("1", "文件夹", true, null, null, null, null),
            new EditorSelectOption("2", "文件名", true, null, null, null, null),
            new EditorSelectOption("3", "文件夹和文件名", true, null, null, null, null)
        };

        public OrganizationUI()
        {
        }

        public OrganizationUI(PluginConfiguration config)
        {
            var options = config.JavOrganizationOptions ?? new JavOrganizationOptions();
            WatchLocation = options.WatchLocations?.FirstOrDefault() ?? string.Empty;
            TargetLocation = options.TargetLocation;
            MinFileSizeMb = options.MinFileSizeMb;
            MovieFolderPattern = options.MovieFolderPattern;
            MoviePattern = options.MoviePattern;
            AddChineseSubtitleSuffix = options.AddChineseSubtitleSuffix;
            CopyOriginalFile = options.CopyOriginalFile;
            OverwriteExistingFiles = options.OverwriteExistingFiles;
            LeftOverFileExtensionsText = string.Join(";", options.LeftOverFileExtensionsToDelete ?? Array.Empty<string>());
            DeleteEmptyFolders = options.DeleteEmptyFolders;
            ExtendedClean = options.ExtendedClean;
        }

        [DisplayName("源文件夹")]
        [Description("该文件夹内的视频将被处理；必须已加入媒体库并完成采集。")]
        [EditFolderPicker]
        public string WatchLocation { get; set; }

        [DisplayName("目标位置")]
        [Description("电影文件将被复制或移动到该文件夹内。")]
        [EditFolderPicker]
        public string TargetLocation { get; set; }

        [DisplayName("最小视频文件大小 MB")]
        [Description("小于该值的视频文件将被忽略。")]
        [MinValue(1)]
        public int MinFileSizeMb { get; set; }

        [DisplayName("影片文件夹表达式")]
        [Description("支持与标题格式相同的变量。")]
        public string MovieFolderPattern { get; set; }

        [DisplayName("影片名表达式")]
        [Description("支持与标题格式相同的变量。")]
        public string MoviePattern { get; set; }

        [DisplayName("增加中文字幕后缀")]
        [Description("如果影片类别包含中文字幕，或源文件夹/文件名以 -C 结尾，则增加 -C 后缀。")]
        [SelectItemsSource(nameof(ChineseSubtitleSuffixOptions))]
        public int AddChineseSubtitleSuffix { get; set; }

        [DisplayName("复制原始文件")]
        [Description("关闭时会移动原始文件。")]
        public bool CopyOriginalFile { get; set; }

        [DisplayName("覆盖已存在文件")]
        [Description("危险：目前无法处理分章节电影，启用后可能覆盖章节文件。")]
        public bool OverwriteExistingFiles { get; set; }

        [DisplayName("清理扩展名")]
        [Description("整理后删除这些扩展名的剩余文件，多个值用分号分隔。")]
        public string LeftOverFileExtensionsText { get; set; }

        [DisplayName("删除空文件夹")]
        public bool DeleteEmptyFolders { get; set; }

        [DisplayName("扩展清理剩余文件")]
        public bool ExtendedClean { get; set; }
    }
}
#endif
