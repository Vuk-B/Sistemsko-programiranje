using System.Collections.Concurrent;
using System.Diagnostics;

namespace WebServer
{
    public static class CacheService
    {
        private static readonly ConcurrentDictionary<string, CachedResponse> _storage = new();
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private static readonly TimeSpan _ttl = TimeSpan.FromMinutes(3);

        private static bool TryGet(string key, out CachedResponse? response)
        {
            if (_storage.TryGetValue(key, out response))
            {
                if (DateTime.Now - response.CreatedAt <= _ttl)
                    return true;

                _storage.TryRemove(key, out _);
                response = null;
                return false;
            }

            response = null;
            return false;
        }

        public static async Task<CachedResponse> GetOrAddAsync(string key, Func<string, Task<string>> factory, CancellationToken ct)
        {
            if (TryGet(key, out var existing))
            {
                Logger.Info($"HIT: {key}");
                return new CachedResponse(existing!.Value, existing.ProcessingTime, isFromCache: true);
            }

            Logger.Info($"MISS: {key}");

            var fileLock = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

            bool acquired = false;
            try
            {
                await fileLock.WaitAsync(ct);
                acquired = true;

                if (TryGet(key, out var delayed))
                {
                    Logger.Info($"HIT (iz kesa nakon obrade druge niti): {key}");
                    return new CachedResponse(delayed!.Value, delayed.ProcessingTime, isFromCache: true);
                }

                var sw = Stopwatch.StartNew();
                string result = await factory(key);
                sw.Stop();

                var entry = new CachedResponse(result, sw.ElapsedMilliseconds);
                _storage[key] = entry;
                return entry;
            }
            finally
            {
                if (acquired)
                    fileLock.Release();
            }
        }
    }
}
