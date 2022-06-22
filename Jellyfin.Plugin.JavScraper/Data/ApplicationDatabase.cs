using System;
using System.IO;
using Jellyfin.Plugin.JavScraper.Scrapers.Model;
using LiteDB;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.JavScraper.Data
{
    public class ApplicationDatabase : LiteDatabase
    {
        public ApplicationDatabase(IApplicationPaths applicationPaths)
            : this(Path.Combine(applicationPaths.DataPath, $"{Constants.PluginName}.db"))
        {
        }

        public ApplicationDatabase(string connectString)
            : base(connectString)
        {
            Overview = GetCollection<Overview>("Overview");
            Metadata = GetCollection<Metadata>("Metadata");
            Translations = GetCollection<Translation>("Translations");
            ImageFaceCenterPoints = GetCollection<ImageFaceCenterPoint>("ImageFaceCenterPoints");

            Overview.EnsureIndex(o => o.Num);
            Overview.EnsureIndex(o => o.Provider);

            Metadata.EnsureIndex(o => o.Num);
            Metadata.EnsureIndex(o => o.Provider);
            Metadata.EnsureIndex(o => o.Url);

            Translations.EnsureIndex(o => o.Hash);
            Translations.EnsureIndex(o => o.Lang);
        }

        /// <summary>
        /// 影片情节信息
        /// </summary>
        public ILiteCollection<Overview> Overview { get; }

        /// <summary>
        /// 元数据
        /// </summary>
        public ILiteCollection<Metadata> Metadata { get; }

        /// <summary>
        /// 翻译
        /// </summary>
        public ILiteCollection<Translation> Translations { get; }

        /// <summary>
        /// 图片人脸中心点位置
        /// </summary>
        public ILiteCollection<ImageFaceCenterPoint> ImageFaceCenterPoints { get; }

        /// <summary>
        /// 保存视频元数据
        /// </summary>
        /// <returns></returns>
        public bool SaveJavVideo(JavVideo video)
        {
            try
            {
                var metaData = Metadata.FindOne(o => o.Url == video.Url && o.Provider == video.Provider);
                var now = DateTime.Now;
                if (metaData == null)
                {
                    metaData = new Metadata()
                    {
                        Created = now,
                        Modified = now,
                        Selected = now,
                        Num = video.Num,
                        Provider = video.Provider,
                        Data = video,
                        Url = video.Url
                    };
                    Metadata.Insert(metaData);
                }
                else
                {
                    metaData.Modified = now;
                    metaData.Selected = now;
                    metaData.Num = video.Num;
                    metaData.Provider = video.Provider;
                    metaData.Data = video;
                    Metadata.Update(metaData);
                }
            }
            catch
            {
            }

            return false;
        }

        /// <summary>
        /// 查找视频元数据
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="url"></param>
        /// <returns></returns>
        public JavVideo? FindJavVideo(string provider, string url)
        {
            return FindMetadata(provider, url)?.Data;
        }

        /// <summary>
        /// 查找视频元数据
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="url"></param>
        /// <returns></returns>
        public Metadata? FindMetadata(string provider, string url) =>
            string.IsNullOrWhiteSpace(provider)
                ? Metadata.FindOne(o => o.Url == url)
                : Metadata.FindOne(o => o.Url == url && o.Provider == provider);
    }
}
