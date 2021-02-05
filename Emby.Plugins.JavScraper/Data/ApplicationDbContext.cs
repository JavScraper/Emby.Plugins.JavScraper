using Emby.Plugins.JavScraper.Scrapers;
using LiteDB;
using MediaBrowser.Common.Configuration;
using System;
using System.IO;

namespace Emby.Plugins.JavScraper.Data
{
    /// <summary>
    /// 数据库访问实体
    /// </summary>
    public class ApplicationDbContext : LiteDatabase
    {
        /// <summary>
        /// 影片情节信息
        /// </summary>
        public ILiteCollection<Plot> Plots { get; }

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
        /// 构造
        /// </summary>
        /// <param name="connectionString"></param>
        public ApplicationDbContext(string connectionString)
            : base(connectionString)
        {
            Plots = GetCollection<Plot>("Plots");
            Metadata = GetCollection<Metadata>("Metadata");
            Translations = GetCollection<Translation>("Translations");
            ImageFaceCenterPoints = GetCollection<ImageFaceCenterPoint>("ImageFaceCenterPoints");

            Plots.EnsureIndex(o => o.num);
            Plots.EnsureIndex(o => o.provider);

            Metadata.EnsureIndex(o => o.num);
            Metadata.EnsureIndex(o => o.provider);
            Metadata.EnsureIndex(o => o.url);

            Translations.EnsureIndex(o => o.hash);
            Translations.EnsureIndex(o => o.lang);
        }

        /// <summary>
        /// 创建数据库实体
        /// </summary>
        /// <param name="applicationPaths"></param>
        /// <returns></returns>
        public static ApplicationDbContext Create(IApplicationPaths applicationPaths)
        {
            var path = Path.Combine(applicationPaths.DataPath, "JavScraper.db");

            try
            {
                return new ApplicationDbContext(path);
            }
            catch { }

            return default;
        }

        /// <summary>
        /// 保存视频元数据
        /// </summary>
        /// <returns></returns>
        public bool SaveJavVideo(JavVideo video)
        {
            try
            {
                var d = Metadata.FindOne(o => o.url == video.Url && o.provider == video.Provider);
                var dt = DateTime.Now;
                if (d == null)
                {
                    d = new Data.Metadata()
                    {
                        created = dt,
                        data = video,
                        modified = dt,
                        num = video.Num,
                        provider = video.Provider,
                        url = video.Url,
                        selected = dt
                    };
                    Metadata.Insert(d);
                }
                else
                {
                    d.modified = dt;
                    d.selected = dt;
                    d.num = video.Num;
                    d.data = video;
                    Metadata.Update(d);
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
        public JavVideo FindJavVideo(string provider, string url)
        {
            if (string.IsNullOrWhiteSpace(provider))
                return Metadata.FindOne(o => o.url == url)?.data;
            else
                return Metadata.FindOne(o => o.url == url && o.provider == provider)?.data;
        }

        /// <summary>
        /// 查找视频元数据
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="url"></param>
        /// <returns></returns>
        public Metadata FindMetadata(string provider, string url)
        {
            if (string.IsNullOrWhiteSpace(provider))
                return Metadata.FindOne(o => o.url == url);
            else
                return Metadata.FindOne(o => o.url == url && o.provider == provider);
        }
    }
}