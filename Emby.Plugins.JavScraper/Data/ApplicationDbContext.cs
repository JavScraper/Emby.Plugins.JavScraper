using LiteDB;
using MediaBrowser.Common.Configuration;
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
        /// 构造
        /// </summary>
        /// <param name="connectionString"></param>
        public ApplicationDbContext(string connectionString)
            : base(connectionString)
        {
            Plots = GetCollection<Plot>("Plots");
            Metadata = GetCollection<Metadata>("Metadata");

            Plots.EnsureIndex(o => o.num);
            Plots.EnsureIndex(o => o.provider);

            Metadata.EnsureIndex(o => o.num);
            Metadata.EnsureIndex(o => o.provider);
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
    }
}