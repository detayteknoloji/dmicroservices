using DMicroservices.Utils.Logger;
using MessagePack;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Reflection;

namespace DMicroservices.DataAccess.Cache
{
    public class MemoryCacheManager
    {
        private IMemoryCache _memoryCache;
        private bool _memoryCacheDisabled = false;
        #region Singleton Section

        private static readonly Lazy<MemoryCacheManager> _instance = new Lazy<MemoryCacheManager>(() => new MemoryCacheManager());

        protected MemoryCacheManager()
        {

            _memoryCache = new MemoryCache(new MemoryDistributedCacheOptions()
            {
                SizeLimit = null
            });
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

            foreach (var keyItem in GetAllKeys())
            {
                if (keyItem.StartsWith(key))
                    _memoryCache.Remove(keyItem);
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

            foreach (var key in GetAllKeys())
            {
                _memoryCache.Remove(key);
            }

            return true;
        }

        /// <summary>
        /// Önbellekte veriyi, verilmişse istenilen süre kadar tutar
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expireTime"></param>
        public bool Set(string key, string value, TimeSpan? expireTime = null)
        {
            if (_memoryCacheDisabled)
                return true;

            if (expireTime.HasValue)
                _memoryCache.Set(key, value, expireTime.Value);
            else
                _memoryCache.Set(key, value);

            return true;
        }

        /// <summary>
        /// Önbellekte veriyi, verilmişse istenilen süre kadar tutar
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expireTime"></param>
        public bool Set(Dictionary<string, string> bulkInsertList)
        {
            if (_memoryCacheDisabled)
                return true;

            foreach (var (key, value) in bulkInsertList)
            {
                _memoryCache.Set(key, value);
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

            if (expireTime.HasValue)
                _memoryCache.Set(key, value, expireTime.Value);
            else
                _memoryCache.Set(key, value);


            return false;
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

            var field = typeof(MemoryCache).GetProperty("EntriesCollection", BindingFlags.NonPublic | BindingFlags.Instance);
            var collection = field.GetValue(_memoryCache) as ICollection;
            var items = new List<string>();
            if (collection != null)
                foreach (var item in collection)
                {
                    var methodInfo = item.GetType().GetProperty("Key");
                    var val = methodInfo.GetValue(item);
                    items.Add(val.ToString());
                }

            return items;
        }

        public void DisableCache()
        {
            _memoryCacheDisabled = false;
        }
        public void EnableCache()
        {
            _memoryCacheDisabled = true;
        }
    }
}
