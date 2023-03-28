using DMicroservices.Utils.Logger;
using MessagePack;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace DMicroservices.DataAccess.Redis
{
    public class RedisManager
    {
        private static readonly string _redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");

        private static IConnectionMultiplexer ConnectionObject;

        /// <summary>
        ///Lock
        /// </summary>
        private static readonly object _lockObj = new object();
        private static readonly object _lockObjFactory = new object();
        private RedLockFactory RedLockFactory { get; set; }

        private ConnectionMultiplexer Connection
        {
            get
            {
                if (ConnectionObject == null || !ConnectionObject.IsConnected)
                {
                    lock (_lockObj)
                    {
                        if (ConnectionObject == null || !ConnectionObject.IsConnected)
                        {
                            ConnectionObject = ConnectionMultiplexer.Connect(Options);
                        }
                    }
                }
                return (ConnectionMultiplexer)ConnectionObject;
            }
        }

        private ConfigurationOptions Options { get; set; }


        #region Singleton Section
        private static readonly Lazy<RedisManager> _instance = new Lazy<RedisManager>(() => new RedisManager());

        private RedisManager()
        {
            Options = ConfigurationOptions.Parse(_redisUrl);
            Options.KeepAlive = 4;
            Options.SyncTimeout = 15000;
            Options.AbortOnConnectFail = false;
            ConnectionObject = ConnectionMultiplexer.Connect(Options);
            //Connection.GetDatabase() = Connection.GetDatabase();
            AddRegisterEvent();
            MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard.WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);
        }

        private void GetConnection()
        {

        }

        public static RedisManager Instance => _instance.Value;
        #endregion

        /// <summary>
        /// Redis önbellekte tutulan veriyi getirir.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string Get(string key)
        {
            return Connection.GetDatabase().StringGet(key);
        }

        /// <summary>
        /// Önbellekte tutulan veriyi siler.
        /// </summary>
        /// <param name="key"></param>
        public bool DeleteByKey(string key)
        {
            return Connection.GetDatabase().KeyDelete(key);
        }

        /// <summary>
        /// Önbellekte tutulan verileri key benzerliğine göre siler.
        /// </summary>
        /// <param name="key"></param>
        public bool DeleteByKeyLike(string key)
        {
            List<RedisKey> keys = GetAllKeys();
            if (keys.Any(p => p.ToString().Contains(key)))
            {
                keys.Where(p => p.ToString().Contains(key)).ToList().ForEach(p => DeleteByKey(p));
                return true;
            }

            return false;
        }

        public bool DeleteByKeyLikeWithRegex(string key, string regexPattern)
        {
            List<RedisKey> keys = GetAllKeys();
            if (keys.Any(p => Regex.IsMatch(p.ToString(), regexPattern)))
            {
                keys.Where(p => Regex.IsMatch(p.ToString(), regexPattern)).ToList().ForEach(p => DeleteByKey(p));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Önbellekte tutulan verileri ön ekine göre siler.
        /// </summary>
        /// <param name="key"></param>
        public bool DeleteByPrefix(string prefix)
        {
            List<RedisKey> keys = GetAllKeys();
            if (keys.Any(p => p.ToString().StartsWith(prefix)))
            {
                keys.Where(p => p.ToString().StartsWith(prefix)).ToList().ForEach(p => DeleteByKey(p));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tüm listeyi temizler
        /// </summary>
        public bool Clear()
        {
            var list = GetAllKeys();
            list.ForEach(p => DeleteByKey(p));
            return list.Any();
        }

        /// <summary>
        /// Async olarak tutulan veriyi getirir.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expireTime"></param>
        public bool SetAsync(string key, string value, TimeSpan expireTime)
        {
            var task = Connection.GetDatabase().StringSetAsync(key, value, expireTime);
            return task.Result;
        }

        /// <summary>
        /// Önbellekte veriyi, verilmişse istenilen süre kadar tutar
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expireTime"></param>
        public bool Set(string key, string value, TimeSpan? expireTime = null)
        {
            if (expireTime > TimeSpan.MinValue)
                return Connection.GetDatabase().StringSet(key, value, expireTime);
            return Connection.GetDatabase().StringSet(key, value);
        }

        /// <summary>
        /// Önbellekte byte[] tipinde veriyi tutar.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public bool SetSerializeBytes<T>(string key, T value, TimeSpan? expiry = null)
        {
            byte[] obj = Serialize<T>(value);
            return Connection.GetDatabase().StringSet(key, obj, expiry);
        }

        /// <summary>
        /// Önbellekte tutulan byte[] tipinde veriyi döner.
        /// </summary>
        /// <param name="key"></param>
        public T GetDeserializeBytes<T>(string key)
        {
            if (Exists(key))
            {
                RedisValue redisValue = Connection.GetDatabase().StringGet(key);
                if (redisValue != RedisValue.Null && redisValue.HasValue)
                    return Deserialize<T>(redisValue);
            }

            return default(T);
        }

        /// <summary>
        /// Anahtara göre var olup olmadığını döner
        /// </summary>
        /// <param name="key"></param>
        public bool Exists(string key)
        {
            List<RedisKey> keys = GetAllKeys();
            return keys.Contains(key);
        }

        /// <summary>
        /// Anahtara göre var olun önbelleği döner
        /// </summary>
        /// <param name="key"></param>
        public bool GetIfExists(string key, out string obj)
        {
            obj = null;

            if (this.Exists(key))
            {
                obj = this.Connection.GetDatabase().StringGet(key);
            }

            return (obj != null);
        }

        /// <summary>
        /// Anahtara göre var olun önbelleği döner
        /// </summary>
        /// <param name="key"></param>
        public bool GetIfExists<T>(string key, out T obj) where T : class
        {
            obj = null;

            if (this.Exists(key))
            {
                obj = this.GetDeserializeBytes<T>(key);
                if (obj == null)
                {
                    this.DeleteByKey(key);
                    return false;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Anahtara göre var olun önbelleği döner
        /// </summary>
        /// <param name="key"></param>
        public bool GetIfExistsObj(string key, out object obj)
        {
            obj = null;

            if (this.Exists(key))
            {
                obj = this.Get(key);
                if (obj == null)
                {
                    this.DeleteByKey(key);
                    return false;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Anahtarın benzerliğine göre var olup olmadığını döner
        /// </summary>
        /// <param name="key"></param>
        public bool ExistsLike(string key)
        {
            List<RedisKey> keys = GetAllKeysByLike(key);
            return keys.Any();
        }

        /// <summary>
        /// Anahtarın benzerliğine göre var olup olmadığını döner
        /// </summary>
        /// <param name="key"></param>
        public bool ExistsPrefixAndLike(string prefix, List<string> subTextList)
        {
            List<RedisKey> keys = GetAllKeys();
            var list = keys.Where(p => p.ToString().StartsWith(prefix)).Select(p => p.ToString().Replace(prefix, ""));
            return list.Any(p => subTextList.Contains(p));
        }

        /// <summary>
        /// Önbellekte bulunan verilerin anahtar listesini getirir.
        /// </summary>
        /// <returns></returns>
        public List<RedisKey> GetAllKeys()
        {
            return Connection.GetServer(Connection.GetEndPoints().Last()).Keys(pattern: "*").ToList();
        }

        /// <summary>
        /// Önbellekte bulunan verilerin benzerliğine göre anahtar listesini getirir.
        /// </summary>
        /// <returns></returns>
        public List<RedisKey> GetAllKeysByLike(string key)
        {
            List<RedisKey> keys = Connection.GetServer(Connection.GetEndPoints().Last()).Keys(pattern: "*").ToList();
            return keys.Where(p => p.ToString().Contains(key)).ToList();
        }

        /// <summary>
        /// Önbellekte bulunan verilerin kalan zamanlarını getirir.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, TimeSpan?> GetAllKeyTime()
        {
            Dictionary<string, TimeSpan?> keyExpireTime = new Dictionary<string, TimeSpan?>();

            List<RedisKey> keys = GetAllKeys();
            foreach (var key in keys)
            {
                keyExpireTime.Add(key, Connection.GetDatabase().KeyTimeToLive(key));
            }

            return keyExpireTime;
        }

        /// <summary>
        /// İstenilen keyin kalan zamanını dönderir.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public TimeSpan? GetKeyTime(string key)
        {
            return Connection.GetDatabase().KeyTimeToLive(key);
        }

        /// <summary>
        /// MessagePack ile veriyi byte[] tipine dönüştürür.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public byte[] Serialize<T>(T obj)
        {
            return MessagePackSerializer.Serialize<T>(obj);
        }

        /// <summary>
        /// Byte[]'i T tipinde dönüştürür.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public T Deserialize<T>(byte[] obj)
        {
            return MessagePackSerializer.Deserialize<T>(obj);
        }

        /// <summary>
        /// Log Event
        /// </summary>
        private void AddRegisterEvent()
        {
            Connection.ConnectionRestored += ConnMultiplexer_ConnectionRestored;
            Connection.ConnectionFailed += ConnMultiplexer_ConnectionFailed;
            Connection.ErrorMessage += ConnMultiplexer_ErrorMessage;
            Connection.ConfigurationChanged += ConnMultiplexer_ConfigurationChanged;
            Connection.HashSlotMoved += ConnMultiplexer_HashSlotMoved;
            Connection.InternalError += ConnMultiplexer_InternalError;
            Connection.ConfigurationChangedBroadcast += ConnMultiplexer_ConfigurationChangedBroadcast;
        }

        /// <summary>
        /// Master-slave konfigürasyon değişikliğinde(Redis ağını bulmak için)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConnMultiplexer_ConfigurationChangedBroadcast(object sender, EndPointEventArgs e)
        {
            ElasticLogger.Instance.Info($"{nameof(ConnMultiplexer_ConfigurationChangedBroadcast)}: {e.EndPoint}");
        }

        /// <summary>
        /// InternalServer Error
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConnMultiplexer_InternalError(object sender, InternalErrorEventArgs e)
        {
            ElasticLogger.Instance.Error(e.Exception, $"{nameof(ConnMultiplexer_InternalError)}");
        }

        /// <summary>
        /// Mantıksal cluster switch olduğunda
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConnMultiplexer_HashSlotMoved(object sender, HashSlotMovedEventArgs e)
        {
            ElasticLogger.Instance.Info(
                 $"{nameof(ConnMultiplexer_HashSlotMoved)}: {nameof(e.OldEndPoint)}-{e.OldEndPoint} To {nameof(e.NewEndPoint)}-{e.NewEndPoint}");
        }

        /// <summary>
        /// Runtime konfigurasyon değişikliğinde
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConnMultiplexer_ConfigurationChanged(object sender, EndPointEventArgs e)
        {
            ElasticLogger.Instance.Info($"{nameof(ConnMultiplexer_ConfigurationChanged)}: {e.EndPoint}");
        }

        /// <summary>
        /// Herhangi bir redis hatasında
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConnMultiplexer_ErrorMessage(object sender, RedisErrorEventArgs e)
        {
            ElasticLogger.Instance.Info($"{nameof(ConnMultiplexer_ErrorMessage)}: {e.Message}");
        }

        /// <summary>
        /// Bağlantı fail olduysa
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConnMultiplexer_ConnectionFailed(object sender, ConnectionFailedEventArgs e)
        {
            ElasticLogger.Instance.Error(e.Exception, $"{nameof(ConnMultiplexer_ConnectionFailed)}");
        }

        /// <summary>
        // Bağlantı restore edildiyse
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConnMultiplexer_ConnectionRestored(object sender, ConnectionFailedEventArgs e)
        {
            ElasticLogger.Instance.Error(e.Exception, $"{nameof(ConnMultiplexer_ConnectionRestored)}");
        }

        public void WaitForLock(string lockName, TimeSpan? delayTimeSpan = null, TimeSpan? lockTimeout = null, TimeSpan? keyExpireTimeSpan = null)
        {
            if (delayTimeSpan == null)
                delayTimeSpan = TimeSpan.FromMilliseconds(100);

            if (keyExpireTimeSpan == null)
                keyExpireTimeSpan = TimeSpan.FromSeconds(30);

            if (lockTimeout == null)
                lockTimeout = TimeSpan.FromSeconds(5);

            if (!Exists($"LOCK-{lockName}"))
            {
                Set($"LOCK-{lockName}", "", keyExpireTimeSpan);
                return;
            }

            var startedDate = DateTime.Now;
            while ((DateTime.Now - startedDate).TotalMilliseconds < lockTimeout.Value.TotalMilliseconds)
            {
                if (!Exists($"LOCK-{lockName}"))
                {
                    Set($"LOCK-{lockName}", "", keyExpireTimeSpan);
                    return;
                }
                Thread.Sleep(delayTimeSpan.Value);
            }


            throw new TimeoutException($"Timeout exception, {lockName} cant be unlock.");
        }

        public void Unlock(string lockName)
        {
            DeleteByKey($"LOCK-{lockName}");
        }

        /// <summary>
        /// RedLock implementation
        /// </summary>
        public RedLockFactory GetLockFactory
        {
            get
            {
                if (RedLockFactory != null)
                    return RedLockFactory;

                lock (_lockObjFactory)
                {
                    RedLockFactory = RedLockFactory.Create(new List<RedLockMultiplexer>() { Connection });
                }

                return RedLockFactory;
            }
        }
    }
}
