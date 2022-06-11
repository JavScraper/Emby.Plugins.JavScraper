using System;
using System.IO;
using Jellyfin.Plugin.JavScraper.Scrapers.Model;
using LiteDB;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.JavScraper.Data
{
    /// <summary>
    /// 数据库访问实体
    /// </summary>
    public class ApplicationDbContext : LiteDatabase
    {
        public ApplicationDbContext(IApplicationPaths applicationPaths)
            : this(Path.Combine(applicationPaths.DataPath, "JavScraper.db"))
        {
        }

        public ApplicationDbContext(string connectString)
            : base(connectString)
        {
            Plots = GetCollection<Plot>("Plots");
            Metadata = GetCollection<Metadata>("Metadata");
            Translations = GetCollection<Translation>("Translations");
            ImageFaceCenterPoints = GetCollection<ImageFaceCenterPoint>("ImageFaceCenterPoints");

            Plots.EnsureIndex(o => o.Num);
            Plots.EnsureIndex(o => o.Provider);

            Metadata.EnsureIndex(o => o.Num);
            Metadata.EnsureIndex(o => o.Provider);
            Metadata.EnsureIndex(o => o.Url);

            Translations.EnsureIndex(o => o.Hash);
            Translations.EnsureIndex(o => o.Lang);
        }

        /// <summary>
        /// 影片情节信息
        /// </summary>
        public virtual ILiteCollection<Plot> Plots { get; }

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
        public Metadata? FindMetadata(string provider, string url)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return Metadata.FindOne(o => o.Url == url);
            }
            else
            {
                return Metadata.FindOne(o => o.Url == url && o.Provider == provider);
            }
        }
    }
}
