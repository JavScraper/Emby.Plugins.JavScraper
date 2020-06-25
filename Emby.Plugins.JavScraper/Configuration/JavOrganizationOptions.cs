namespace Emby.Plugins.JavScraper.Configuration
{
    /// <summary>
    /// 组织配置
    /// </summary>
    public class JavOrganizationOptions
    {
        public bool IsEnabled { get; set; }
        public int MinFileSizeMb { get; set; }
        public string[] LeftOverFileExtensionsToDelete { get; set; }
        public string[] WatchLocations { get; set; }

        public string MoviePattern { get; set; }

        public bool OverwriteExistingFiles { get; set; }

        public bool DeleteEmptyFolders { get; set; }

        public bool ExtendedClean { get; set; }

        public bool CopyOriginalFile { get; set; }

        public bool AutoDetectMovie { get; set; }

        public string DefaultMovieLibraryPath { get; set; }

        public bool MovieFolder { get; set; }

        public string MovieFolderPattern { get; set; }

        public JavOrganizationOptions()
        {
            MinFileSizeMb = 50;

            LeftOverFileExtensionsToDelete = new string[] { };

            MoviePattern = "%fn.%ext";

            WatchLocations = new string[] { };

            CopyOriginalFile = false;

            MovieFolder = false;

            MovieFolderPattern = "%mn (%my)";

            ExtendedClean = false;
        }
    }
}