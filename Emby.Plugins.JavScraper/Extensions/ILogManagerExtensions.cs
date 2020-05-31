#if !__JELLYFIN__

using System;

namespace MediaBrowser.Model.Logging
{
    /// <summary>
    /// ILogManager 扩展
    /// </summary>
    public static class ILogManagerExtensions
    {
        /// <summary>
        /// 创建日志记录器
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="factory">日志管理器</param>
        /// <returns></returns>
        public static ILogger CreateLogger<T>(this ILogManager factory)
            => factory.GetLogger(typeof(T).FullName);

        /// <summary>
        /// 创建日志记录器
        /// </summary>
        /// <param name="factory">日志管理器</param>
        /// <param name="type">类型</param>
        /// <returns></returns>
        public static ILogger CreateLogger(this ILogManager factory, Type type)
            => factory.GetLogger(type.FullName);
    }
}

#endif