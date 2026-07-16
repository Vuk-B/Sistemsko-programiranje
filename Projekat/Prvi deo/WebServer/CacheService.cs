using System.Collections.Concurrent;

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

        public static string GetOrAdd(string key, Func<string, string> factory)
        {
            if (TryGet(key, out var existing))
            {
                Logger.Info($"HIT: {key}");
                return existing!.Value;
            }

            Logger.Info($"MISS: {key}");

            var fileLock = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

            fileLock.Wait();
            try
            {
                if (TryGet(key, out var delayed))
                {
                    Logger.Info($"HIT (iz kesa nakon obrade druge niti): {key}");
                    return delayed!.Value;
                }

                string result = factory(key);
                _storage[key] = new CachedResponse(result);
                return result;
            }
            finally
            {
                fileLock.Release();
            }
        }
    }
}
