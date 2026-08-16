using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using System.ComponentModel;

namespace Emby.Plugins.JavScraper.Configuration
{
    /// <summary>
    /// 视频文件整理配置
    /// </summary>
    public class JavOrganizationOptions
    {
/// <summary>
        /// 源文件夹
        /// </summary>
        [DisplayNameL("源文件夹")]
        [DescriptionL("需要自动整理的视频来源目录。")]
        public string[] WatchLocations { get; set; }

        /// <summary>
        /// 目标位置
        /// </summary>
        [DisplayNameL("目标位置")]
        [EditFolderPicker]
        public string TargetLocation { get; set; }

        /// <summary>
        /// 最小视频文件大小
        /// </summary>
        [DisplayNameL("最小视频文件大小 MB")]
        [MinValue(1)]
        public int MinFileSizeMb { get; set; }

        /// <summary>
        /// 影片文件夹表达式
        /// </summary>
        [DisplayNameL("影片文件夹表达式")]
        public string MovieFolderPattern { get; set; }

        /// <summary>
        /// 影片名表达式
        /// </summary>
        [DisplayNameL("影片名表达式")]
        public string MoviePattern { get; set; }

        /// <summary>
        /// 增加中文字幕后缀（-C），0不加，1文件夹，2，文件名，3，文件夹和文件名
        /// </summary>
        [DisplayNameL("增加中文字幕后缀")]
        [DescriptionL("0 不加，1 文件夹，2 文件名，3 文件夹和文件名。")]
        [MinValue(0)]
        [MaxValue(3)]
        public int AddChineseSubtitleSuffix { get; set; }


        /// <summary>
        /// 复制或者移动原始文件
        /// </summary>
        [DisplayNameL("复制原始文件")]
        [DescriptionL("关闭时会移动原始文件。")]
        public bool CopyOriginalFile { get; set; }

        /// <summary>
        /// 覆盖已存在的文件
        /// </summary>
        [DisplayNameL("覆盖已存在文件")]
        public bool OverwriteExistingFiles { get; set; }

        /// <summary>
        /// 删除以下扩展名的文件
        /// </summary>
        [DisplayNameL("清理扩展名")]
        [DescriptionL("整理后删除这些扩展名的剩余文件。")]
        public string[] LeftOverFileExtensionsToDelete { get; set; }

        /// <summary>
        /// 删除空文件夹
        /// </summary>
        [DisplayNameL("删除空文件夹")]
        public bool DeleteEmptyFolders { get; set; }

        /// <summary>
        /// 扩展清理剩余的文件
        /// </summary>
        [DisplayNameL("扩展清理剩余文件")]
        public bool ExtendedClean { get; set; }


        public JavOrganizationOptions()
        {
            MinFileSizeMb = 50;
            AddChineseSubtitleSuffix = 3;
            LeftOverFileExtensionsToDelete = new string[] { };
            MovieFolderPattern = "%actor%/%num% %title_original%";
            MoviePattern = "%num%";
            WatchLocations = new string[] { };
            CopyOriginalFile = false;
            DeleteEmptyFolders = true;
            ExtendedClean = false;
        }
    }
}