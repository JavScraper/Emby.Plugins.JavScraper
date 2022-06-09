using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.JavScraper.Configuration
{
    /// <summary>
    /// 视频文件整理配置
    /// </summary>
    public class JavOrganizationOptions
    {
        public JavOrganizationOptions()
        {
            MinFileSizeMb = 50;
            AddChineseSubtitleSuffix = 3;
            LeftOverFileExtensionsToDelete = new List<string>();
            MovieFolderPattern = "%actor%/%num% %title_original%";
            MoviePattern = "%num%";
            WatchLocations = new List<string>();
            CopyOriginalFile = false;
            DeleteEmptyFolders = true;
            ExtendedClean = false;
            TargetLocation = string.Empty;
        }

        /// <summary>
        /// 源文件夹
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:不要公开泛型列表", Justification = "<挂起>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:集合属性应为只读", Justification = "<挂起>")]
        public List<string> WatchLocations { get; set; }

        /// <summary>
        /// 目标位置
        /// </summary>
        public string TargetLocation { get; set; }

        /// <summary>
        /// 最小视频文件大小
        /// </summary>
        public int MinFileSizeMb { get; set; }

        /// <summary>
        /// 影片文件夹表达式
        /// </summary>
        public string MovieFolderPattern { get; set; }

        /// <summary>
        /// 影片名表达式
        /// </summary>
        public string MoviePattern { get; set; }

        /// <summary>
        /// 增加中文字幕后缀（-C），0不加，1文件夹，2，文件名，3，文件夹和文件名
        /// </summary>
        public int AddChineseSubtitleSuffix { get; set; }

        /// <summary>
        /// 复制或者移动原始文件
        /// </summary>
        public bool CopyOriginalFile { get; set; }

        /// <summary>
        /// 覆盖已存在的文件
        /// </summary>
        public bool OverwriteExistingFiles { get; set; }

        /// <summary>
        /// 删除以下扩展名的文件
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:不要公开泛型列表", Justification = "<挂起>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:集合属性应为只读", Justification = "<挂起>")]
        public List<string> LeftOverFileExtensionsToDelete { get; set; }

        /// <summary>
        /// 删除空文件夹
        /// </summary>
        public bool DeleteEmptyFolders { get; set; }

        /// <summary>
        /// 扩展清理剩余的文件
        /// </summary>
        public bool ExtendedClean { get; set; }
    }
}
