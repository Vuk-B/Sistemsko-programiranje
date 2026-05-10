using System.Collections.Concurrent;

namespace WebServer
{
    public static class CacheService
    {
        private static readonly Dictionary<string, (string Value, DateTime Timestamp)> _cache = new();
        private static readonly object _cacheLock = new();

        private static readonly ConcurrentDictionary<string, object> _keyLocks = new();

        private static readonly TimeSpan _ttl = TimeSpan.FromMinutes(60);

        public static string GetOrAdd(string key, Func<string> factory)
        {
            lock (_cacheLock)
            {
                if (TryGetValid(key, out string? value))
                {
                    Logger.Log($"Cache HIT: {key}");
                    return value!;
                }
            }

            Logger.Log($"Cache MISS: {key}");

            object keyLock = _keyLocks.GetOrAdd(key, _ => new object());
            lock (keyLock)
            {
                lock (_cacheLock)
                {
                    if (TryGetValid(key, out string? value))
                    {
                        Logger.Log($"Cache HIT (nakon cekanja): {key}");
                        return value!;
                    }
                }

                string result = factory();

                lock (_cacheLock)
                {
                    _cache[key] = (result, DateTime.Now);
                }

                return result;
            }
        }

        private static bool TryGetValid(string key, out string? value)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (DateTime.Now - entry.Timestamp < _ttl)
                {
                    value = entry.Value;
                    return true;
                }
            }
            value = null;
            return false;
        }
    }
}
