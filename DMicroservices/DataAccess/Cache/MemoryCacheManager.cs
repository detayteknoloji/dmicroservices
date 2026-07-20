using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace DMicroservices.DataAccess.Cache
{
    public class MemoryCacheManager
    {
        private readonly IMemoryCache _memoryCache;
        private bool _memoryCacheDisabled = false;

        /// <summary>
        /// Cachede tutulan keyler
        /// </summary>
        private readonly ConcurrentDictionary<string, byte> _keys = new ConcurrentDictionary<string, byte>();

        /// <summary>
        /// memcachede tutulacak keylerin default ttl değeri
        /// </summary>
        private static readonly TimeSpan DefaultExpireTime = TimeSpan.FromSeconds(GetEnvironmentLong("MEMORY_CACHE_DEFAULT_TTL", 600));

        #region Singleton Section

        private static readonly Lazy<MemoryCacheManager> _instance = new Lazy<MemoryCacheManager>(() => new MemoryCacheManager());

        protected MemoryCacheManager()
        {
            var options = new MemoryCacheOptions()
            {
                SizeLimit = null,
                ExpirationScanFrequency = TimeSpan.FromMinutes(1)
            };

            _memoryCache = new MemoryCache(options);
        }

        public static MemoryCacheManager Instance => _instance.Value;
        #endregion

        /// <summary>
        /// Bellekte tutulan veriyi getirir.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string Get(string key)
        {
            if (_memoryCacheDisabled)
                return null;
            return _memoryCache.Get(key)?.ToString();
        }

        /// <summary>
        /// Önbellekte tutulan veriyi siler.
        /// </summary>
        /// <param name="key"></param>
        public bool DeleteByKey(string key)
        {
            if (_memoryCacheDisabled)
                return true;

            _memoryCache.Remove(key);
            _keys.TryRemove(key, out _);
            return true;
        }

        /// <summary>
        /// Önbellekte tutulan verileri key benzerliğine göre siler.
        /// </summary>
        /// <param name="key"></param>
        public bool DeleteByKeyLike(string key)
        {
            if (_memoryCacheDisabled)
                return true;

            foreach (var keyItem in _keys.Keys.ToList())
            {
                if (keyItem.StartsWith(key))
                {
                    _memoryCache.Remove(keyItem);
                    _keys.TryRemove(keyItem, out _);
                }
            }

            return true;
        }

        /// <summary>
        /// Tüm listeyi temizler
        /// </summary>
        public bool Clear()
        {
            if (_memoryCacheDisabled)
                return true;

            foreach (var key in _keys.Keys.ToList())
            {
                _memoryCache.Remove(key);
                _keys.TryRemove(key, out _);
            }

            return true;
        }

        /// <summary>
        /// Önbellekte veriyi, verilmişse istenilen süre kadar tutar; süre verilmezse varsayılan yaşam süresi uygulanır.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expireTime"></param>
        public bool Set(string key, string value, TimeSpan? expireTime = null)
        {
            if (_memoryCacheDisabled)
                return true;

            _memoryCache.Set(key, value, CreateEntryOptions(value, expireTime));
            _keys.TryAdd(key, 0);

            return true;
        }

        /// <summary>
        /// Önbellekte verilen listedeki verileri varsayılan yaşam süresiyle tutar.
        /// </summary>
        /// <param name="bulkInsertList"></param>
        public bool Set(Dictionary<string, string> bulkInsertList)
        {
            if (_memoryCacheDisabled)
                return true;

            foreach (var (key, value) in bulkInsertList)
            {
                _memoryCache.Set(key, value, CreateEntryOptions(value, null));
                _keys.TryAdd(key, 0);
            }

            return true;
        }

        /// <summary>
        /// Önbellekte byte[] tipinde veriyi tutar.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public bool SetSerializeBytes<T>(string key, T value, TimeSpan? expireTime = null)
        {
            if (_memoryCacheDisabled)
                return true;

            _memoryCache.Set(key, value, CreateEntryOptions(value, expireTime));
            _keys.TryAdd(key, 0);

            return true;
        }

        /// <summary>
        /// Önbellekte tutulan byte[] tipinde veriyi döner.
        /// </summary>
        /// <param name="key"></param>
        public T GetDeserializeBytes<T>(string key)
        {
            if (_memoryCacheDisabled)
                return default;

            return _memoryCache.Get<T>(key);
        }

        /// <summary>
        /// Anahtara göre var olup olmadığını döner
        /// </summary>
        /// <param name="key"></param>
        public bool Exists(string key)
        {
            if (_memoryCacheDisabled)
                return false;

            return _memoryCache.Get(key) != null;
        }

        /// <summary>
        /// Anahtara göre var olun önbelleği döner
        /// </summary>
        /// <param name="key"></param>
        public bool GetIfExists(string key, out string obj)
        {
            if (_memoryCacheDisabled)
            {
                obj = null;
                return false;
            }

            obj = _memoryCache.Get(key)?.ToString();
            return obj != null;
        }

        /// <summary>
        /// Anahtara göre var olun önbelleği döner
        /// </summary>
        /// <param name="key"></param>
        public bool GetIfExists<T>(string key, out T obj) where T : class
        {
            if (_memoryCacheDisabled)
            {
                obj = null;
                return false;
            }

            var memoryCacheData = _memoryCache.Get(key);

            obj = (T)memoryCacheData;
            return memoryCacheData != null;
        }

        public List<string> GetAllKeys()
        {
            if (_memoryCacheDisabled)
            {
                return null;
            }

            return _keys.Keys.ToList();
        }

        public void DisableCache()
        {
            _memoryCacheDisabled = true;
        }
        public void EnableCache()
        {
            _memoryCacheDisabled = false;
        }

        private MemoryCacheEntryOptions CreateEntryOptions(object value, TimeSpan? expireTime)
        {
            var options = new MemoryCacheEntryOptions()
            {
                AbsoluteExpirationRelativeToNow = expireTime ?? DefaultExpireTime
            };

            options.RegisterPostEvictionCallback((evictedKey, evictedValue, reason, state) =>
            {
                // keyin ttl i bittiğinde dictten de replace oldugu hariç değişssin
                if (reason != EvictionReason.Replaced)
                    _keys.TryRemove(evictedKey.ToString(), out _);
            });

            return options;
        }

        private static long GetEnvironmentLong(string name, long defaultValue)
        {
            string envValue = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(envValue) && long.TryParse(envValue, out long parsedValue) && parsedValue > 0)
                return parsedValue;
            return defaultValue;
        }
    }
}
