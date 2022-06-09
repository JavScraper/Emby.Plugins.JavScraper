using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.JavScraper.Extensions
{
    /// <summary>
    /// 命名锁
    /// </summary>
    public class NamedAsyncLocker
    {
        private readonly ConcurrentDictionary<string, Lock> _lockDict = new();

        public async Task<IDisposable> WaitAsync(string name)
        {
            var item = _lockDict.GetOrAdd(name, s => new Lock(this, name));
            await item.WaitAsync().ConfigureAwait(false);
            return item;
        }

        private sealed class Lock : IDisposable
        {
            private readonly NamedAsyncLocker _named_locker;
            private readonly string _name;
            private readonly SemaphoreSlim _semaphoreSlim;

            internal Lock(NamedAsyncLocker named_locker, string name)
            {
                _named_locker = named_locker;
                _name = name;
                _semaphoreSlim = new SemaphoreSlim(1, 1);
            }

            internal int RefCount { get; set; }

            public Task WaitAsync()
            {
                RefCount++;
                return _semaphoreSlim.WaitAsync();
            }

            public void Dispose()
            {
                RefCount--;
                if (RefCount == 0)
                {
                    _semaphoreSlim.Dispose();
                    _named_locker._lockDict.TryRemove(_name, out var o);
                    GC.SuppressFinalize(this);
                }
            }
        }
    }
}
