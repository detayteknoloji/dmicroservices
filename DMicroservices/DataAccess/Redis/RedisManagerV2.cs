using DMicroservices.Utils.Logger;
using MessagePack;
using MongoDB.Driver;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DMicroservices.DataAccess.Redis
{
    public class RedisManagerV2
    {
        private static readonly string _redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
        private static readonly string _containerPodName = Environment.GetEnvironmentVariable("POD_NAME") ??
                                                             Environment.MachineName ??
                                                             Guid.NewGuid().ToString("N")[..8];
        // redis connection nesneleri
        private static volatile IConnectionMultiplexer _connectionMultiplexer;
        private static readonly object _connectionLock = new object();
        private static readonly object _lockObjFactory = new object();

        // redis'e en son ne zaman bağlandı ? 
        private static DateTime _lastConnectionAttempt = DateTime.MinValue;
        // son bağlanmasının üzerinden eğer connection fail ise tekrar ne zaman bağlanmayı denesin ?
        private static readonly TimeSpan _reconnectInterval = TimeSpan.FromSeconds(30);

        private RedLockFactory RedLockFactory { get; set; }

        #region Singleton section
        private static readonly Lazy<RedisManagerV2> _instance =
            new Lazy<RedisManagerV2>(() => new RedisManagerV2());

        private RedisManagerV2()
        {
            LogInfo($"Container {_containerPodName}: Initializing Redis Manager.");

            InitializeRedisConnection();

            MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard.WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);

            AppDomain.CurrentDomain.ProcessExit += OnContainerShutdown;
            Console.CancelKeyPress += (s, e) => OnContainerShutdown(s, null);
        }

        public static RedisManagerV2 Instance => _instance.Value;
        #endregion

        #region Connections

        private void InitializeRedisConnection()
        {
            if (string.IsNullOrEmpty(_redisUrl))
            {
                LogInfo($"Container {_containerPodName}: Redis url bilgisi bulunamadı!. Lock ve cache işlemleri gerçekleşmeyecek!");
                return;
            }

            try
            {
                var options = ConfigurationOptions.Parse(_redisUrl);

                // Redis connection ayarları
                options.ClientName = $"Container-{_containerPodName}";
                options.AbortOnConnectFail = false;
                options.ConnectRetry = 3;
                options.ConnectTimeout = 3000;  // 3 saniye (15 yerine)
                options.SyncTimeout = 3000;     // 3 saniye (15 yerine)
                options.AsyncTimeout = 3000;
                options.KeepAlive = 30;         // 30 saniye

                options.DefaultDatabase = 0;

                _connectionMultiplexer = ConnectionMultiplexer.Connect(options);


                RegisterConnectionEvents();
                LogInfo($"Container {_containerPodName}: Redis bağlantısı sağlandı");
            }
            catch (Exception ex)
            {
                LogError($"Container {_containerPodName}: Redis Başlatılırken hata aldı: {ex.Message}", ex);
                _lastConnectionAttempt = DateTime.Now;
            }
        }

        #endregion

        #region Bağlantı ayarlamaları ve retry denemeleri

        private IDatabase GetDatabase(bool isThrowEx)
        {

            if (isThrowEx)
            {
                if ((_connectionMultiplexer == null || !_connectionMultiplexer.IsConnected) && ShouldRetryConnection())
                {
                    lock (_connectionLock)
                    {
                        if ((_connectionMultiplexer == null || !_connectionMultiplexer.IsConnected) && ShouldRetryConnection())
                        {
                            InitializeRedisConnection();
                        }
                    }
                }
                return _connectionMultiplexer.GetDatabase();
            }

            if (!_connectionMultiplexer.IsConnected && ShouldRetryConnection())
            {
                lock (_connectionLock)
                {
                    if (!_connectionMultiplexer.IsConnected && ShouldRetryConnection())
                    {
                        _lastConnectionAttempt = DateTime.Now;
                        InitializeRedisConnection();
                    }
                }
            }

            return _connectionMultiplexer.IsConnected && _connectionMultiplexer?.IsConnected == true
                ? _connectionMultiplexer.GetDatabase()
                : null;
        }

        private bool ShouldRetryConnection()
        {
            return DateTime.Now - _lastConnectionAttempt > _reconnectInterval;
        }

        #endregion

        #region Core Operation - Tüm operasyonlar buradan yönetilir.

        private T ExecuteRedisOperation<T>(Func<IDatabase, T> redisOperation, string key, string operationName, bool isThrowException = false)
        {

            if (string.IsNullOrWhiteSpace(key))
            {
                return default(T);
            }

            try
            {
                var database = GetDatabase(isThrowException);
                if (database != null)
                {
                    var result = redisOperation(database);

                    return result;
                }
            }
            catch (Exception ex)
            {
                LogError($"Container {_containerPodName}: Redis {operationName} '{key}' setlenirken hata aldı: {ex.Message}", ex);

                if (isThrowException)
                    throw;

                return default(T);
            }

            return default(T);
        }

        #endregion

        #region Basic Operation

        /// <summary>
        /// Redis'ten veri getirir. Redis yoksa null döner
        /// </summary>
        public string Get(string key, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    var result = db.StringGet(key);
                    return result.HasValue ? result.ToString() : null;
                },
                key,
                "GET", isThrowException: isThrowEx
            );
        }

        /// <summary>
        /// Redis'e veri kaydeder. 
        /// Redis yoksa: isCritical=true ve hybrid mode açık ise queue'ya alır, değilse false döner.
        /// </summary>
        public bool Set(string key, string value, TimeSpan? expireTime = null, bool isThrowEx = true)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                return false;

            return ExecuteRedisOperation(
                db =>
                {
                    if (expireTime > TimeSpan.MinValue)
                        return db.StringSet(key, value, expireTime);
                    return db.StringSet(key, value);
                },
                key,
                "SET",
                isThrowException: isThrowEx
            );
        }

        /// <summary>
        /// Key'in varlığını kontrol eder. Redis yoksa false döner.
        /// </summary>
        public bool Exists(string key, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db => db.KeyExists(key),
                key,
                "EXISTS", isThrowException: isThrowEx
            );
        }

        /// <summary>
        /// Key'i siler.
        /// Redis yoksa: isCritical=true ve hybrid mode açık ise queue'ya alır.
        /// </summary>
        public bool DeleteByKey(string key, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db => db.KeyDelete(key),
                key,
                "DELETE",
                isThrowException: isThrowEx
            );
        }

        /// <summary>
        /// Object'i serialize ederek Redis'e kaydeder.
        /// </summary>
        public bool SetSerializeBytes<T>(string key, T value, TimeSpan? expiry = null, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    byte[] obj = MessagePack.MessagePackSerializer.Serialize<T>(value);
                    return db.StringSet(key, obj, expiry);
                },
                key,
                "SET_SERIALIZE",
                isThrowException: isThrowEx
            );
        }

        /// <summary>
        /// Object'i deserialize ederek Redis'ten getirir.
        /// </summary>
        public T GetDeserializeBytes<T>(string key, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    RedisValue redisValue = db.StringGet(key);
                    if (redisValue != RedisValue.Null && redisValue.HasValue)
                        return MessagePack.MessagePackSerializer.Deserialize<T>(redisValue);
                    return default(T);
                },
                key,
                "GET_DESERIALIZE", isThrowException: isThrowEx
            );
        }

        #endregion

        #region Db numarasına göre işlemler

        public string Get(string key, int databaseNum, bool isThrowEx = true)
        {
            try
            {
                var database = GetDatabase(isThrowEx);
                if (database != null)
                {
                    var db = _connectionMultiplexer.GetDatabase(databaseNum);
                    var redisResult = db.StringGet(key);
                    return redisResult.HasValue ? redisResult.ToString() : null;
                }
            }
            catch (Exception ex)
            {
                LogError($"Container {_containerPodName}: Redisten '{key}' getirilirken hata aldı! Redis DB numarası {databaseNum}: {ex.Message}", ex);
                if (isThrowEx)
                    throw;
                return null;
            }
            return null;
        }

        public bool Set(string key, string value, int databaseNum, TimeSpan? expireTime = null, bool isThrowEx = true)
        {

            try
            {
                var database = GetDatabase(isThrowEx);
                if (database != null)
                {
                    var db = _connectionMultiplexer.GetDatabase(databaseNum);
                    var result = expireTime > TimeSpan.MinValue
                        ? db.StringSet(key, value, expireTime)
                        : db.StringSet(key, value);

                    return result;
                }
            }
            catch (Exception ex)
            {
                LogError($"Container {_containerPodName}: Redisten '{key}' getirilirken hata aldı! Redis DB numarası {databaseNum}: {ex.Message}", ex);
                if (isThrowEx)
                    throw;

                return false;
            }

            return false;
        }

        public bool DeleteByKey(string key, int databaseNum, bool isThrowEx = true)
        {
            try
            {
                var database = GetDatabase(isThrowEx);
                if (database != null)
                {
                    var db = _connectionMultiplexer.GetDatabase(databaseNum);
                    var result = db.KeyDelete(key);

                    return result;
                }
            }
            catch (Exception ex)
            {
                LogError($"Container {_containerPodName}: Redisten '{key}' silinirken hata aldı! Redis DB numarası {databaseNum}: {ex.Message}", ex);
                if (isThrowEx)
                    throw;

                return false;
            }


            return false;
        }

        #endregion

        #region Bulk operasyonlar

        public bool Set(Dictionary<string, string> bulkInsertList, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    KeyValuePair<RedisKey, RedisValue>[] redisValueArray =
                        new KeyValuePair<RedisKey, RedisValue>[bulkInsertList.Count];

                    int i = 0;
                    foreach (var (key, value) in bulkInsertList)
                    {
                        redisValueArray[i] = new KeyValuePair<RedisKey, RedisValue>(key, value);
                        i++;
                    }

                    return db.StringSet(redisValueArray);
                },
                $"BULK_{bulkInsertList.Count}_ITEMS",
                "BULK_SET",
                isThrowException: isThrowEx
            );
        }

        public bool Set(Dictionary<string, string> bulkInsertList, int databaseNum, bool isThrowEx = true)
        {
            var database = GetDatabase();
            if (database != null)
            {
                try
                {
                    var db = _connectionMultiplexer.GetDatabase(databaseNum);
                    KeyValuePair<RedisKey, RedisValue>[] redisValueArray =
                        new KeyValuePair<RedisKey, RedisValue>[bulkInsertList.Count];

                    int i = 0;
                    foreach (var (key, value) in bulkInsertList)
                    {
                        redisValueArray[i] = new KeyValuePair<RedisKey, RedisValue>(key, value);
                        i++;
                    }

                    var result = db.StringSet(redisValueArray);
                    return result;
                }
                catch (Exception ex)
                {
                    LogError($"Container {_containerPodName}: Redisten bulk insert yapılırken hata aldı! Redis DB numarası {databaseNum}: {ex.Message}", ex);
                    if (isThrowEx)
                        throw;

                    return false;
                }
            }

            return false;
        }

        #endregion

        #region Business operasyonları

        public Tuple<TimeSpan?, string> GetWithExpiry(string key, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    var cacheValue = db.StringGetWithExpiry(key);
                    return new Tuple<TimeSpan?, string>(cacheValue.Expiry, cacheValue.Value.HasValue ? cacheValue.Value.ToString() : null);
                },
                key,
                "GET_WITH_EXPIRY", isThrowException: isThrowEx
            );
        }

        public Tuple<TimeSpan?, T> GetDeserializeBytesWithExpiry<T>(string key, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    RedisValueWithExpiry redisValue = db.StringGetWithExpiry(key);
                    if (redisValue.Value != RedisValue.Null && redisValue.Value.HasValue)
                        return new Tuple<TimeSpan?, T>(redisValue.Expiry, MessagePack.MessagePackSerializer.Deserialize<T>(redisValue.Value));
                    return new Tuple<TimeSpan?, T>(null, default(T));
                },
                key,
                "GET_DESERIALIZE_WITH_EXPIRY", isThrowException: isThrowEx
            );
        }


        public List<RedisKey> GetAllKeys(bool isThrowEx)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    try
                    {
                        return _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().Last()).Keys(pattern: "*").ToList();
                    }
                    catch (Exception ex)
                    {
                        LogError($"GetAllKeys Redis'e erişirken hata aldı: {ex.Message}", ex);
                        return new List<RedisKey>();
                    }
                },
                "*",
                "GET_ALL_KEYS", isThrowException: isThrowEx
            ) ?? new List<RedisKey>();
        }

        public List<RedisKey> GetAllKeys(int databaseNum, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    try
                    {
                        return _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().Last()).Keys(databaseNum, pattern: "*").ToList();
                    }
                    catch (Exception ex)
                    {
                        LogError($"GetAllKeys(DB {databaseNum}) Redis'e erişirken hata aldı: {ex.Message}", ex);
                        return new List<RedisKey>();
                    }
                },
                $"DB{databaseNum}:*",
                "GET_ALL_KEYS", isThrowException: isThrowEx
            ) ?? new List<RedisKey>();
        }

        public List<RedisKey> GetAllKeysByLikeOld(string key, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    try
                    {
                        List<RedisKey> keys = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().Last()).Keys(pattern: "*").ToList();
                        return keys.Where(p => p.ToString().Contains(key)).ToList();
                    }
                    catch (Exception ex)
                    {
                        LogError($"{key}:'ine like eden keyler getirilirken hata aldı! {ex.Message}", ex);
                        return new List<RedisKey>();
                    }
                },
                key,
                "GET_KEYS_BY_LIKE_OLD", isThrowException: isThrowEx
            ) ?? new List<RedisKey>();
        }

        public List<RedisKey> GetAllKeysByLike(string key, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    try
                    {
                        return _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().Last()).Keys(pattern: $"*{key}*").ToList();
                    }
                    catch (Exception ex)
                    {
                        LogError($"{key}:'ine like eden keyler getirilirken hata aldı: {ex.Message}", ex);
                        return new List<RedisKey>();
                    }
                },
                key,
                "GET_KEYS_BY_LIKE", isThrowException: isThrowEx
            ) ?? new List<RedisKey>();
        }

        public List<RedisKey> GetAllKeysByLikeOld(string key, int databaseNum, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    try
                    {
                        List<RedisKey> keys = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().Last()).Keys(databaseNum, pattern: "*").ToList();
                        return keys.Where(p => p.ToString().Contains(key)).ToList();
                    }
                    catch (Exception ex)
                    {
                        LogError($"{key}:'ine like eden keyler getirilirken hata aldı! {ex.Message}", ex);
                        return new List<RedisKey>();
                    }
                }, $"DB{databaseNum}:{key}",
               "GET_KEYS_BY_LIKE_OLD", isThrowException: isThrowEx
           ) ?? new List<RedisKey>();
        }

        public List<RedisKey> GetAllKeysByLike(string key, int databaseNum, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    try
                    {
                        List<RedisKey> keys = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().Last()).Keys(databaseNum, pattern: $"*{key}*").ToList();
                        return keys.Where(p => p.ToString().Contains(key)).ToList();
                    }
                    catch (Exception ex)
                    {
                        LogError($"{key}:'ine like eden keyler getirilirken hata aldı! {ex.Message}", ex);
                        return new List<RedisKey>();
                    }
                }, $"DB{databaseNum}:{key}",
               "GET_KEYS_BY_LIKE", isThrowException: isThrowEx
           ) ?? new List<RedisKey>();
        }

        public bool DeleteByKeyLike(string key, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    List<RedisKey> keys = GetAllKeysByLike(key);
                    var matchingKeys = keys.Where(p => p.ToString().Contains(key)).ToList();
                    if (matchingKeys.Any())
                    {
                        foreach (var matchingKey in matchingKeys)
                        {
                            db.KeyDelete(matchingKey);
                        }
                        return true;
                    }
                    return false;
                },
                key,
                "DELETE_BY_LIKE",
                isThrowEx
            );
        }

        public bool DeleteByKeyLikeOld(string key, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    List<RedisKey> keys = GetAllKeys(isThrowEx);
                    var matchingKeys = keys.Where(p => p.ToString().Contains(key)).ToList();
                    if (matchingKeys.Any())
                    {
                        foreach (var matchingKey in matchingKeys)
                        {
                            db.KeyDelete(matchingKey);
                        }
                        return true;
                    }
                    return false;
                },
                key,
                "DELETE_BY_LIKE_OLD", isThrowEx
            );
        }


        public bool DeleteByPrefix(string prefix, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    List<RedisKey> keys = GetAllKeysByLike(prefix);
                    var matchingKeys = keys.Where(p => p.ToString().StartsWith(prefix)).ToList();
                    if (matchingKeys.Any())
                    {
                        foreach (var matchingKey in matchingKeys)
                        {
                            db.KeyDelete(matchingKey);
                        }
                        return true;
                    }
                    return false;
                },
                prefix,
                "DELETE_BY_PREFIX",
                 isThrowEx
            );
        }

        public bool DeleteByPrefixOld(string prefix, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    List<RedisKey> keys = GetAllKeys(isThrowEx);
                    var matchingKeys = keys.Where(p => p.ToString().StartsWith(prefix)).ToList();
                    if (matchingKeys.Any())
                    {
                        foreach (var matchingKey in matchingKeys)
                        {
                            db.KeyDelete(matchingKey);
                        }
                        return true;
                    }
                    return false;
                },
                prefix,
                "DELETE_BY_PREFIX_OLD",
                isThrowEx

            );
        }

        public bool Clear(bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    var keys = GetAllKeys(isThrowEx);
                    if (keys.Any())
                    {
                        foreach (var key in keys)
                        {
                            db.KeyDelete(key);
                        }
                        return true;
                    }
                    return false;
                },
                "ALL_KEYS",
                "CLEAR"
            );
        }

        public Dictionary<string, TimeSpan?> GetAllKeyTime(bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    Dictionary<string, TimeSpan?> keyExpireTime = new Dictionary<string, TimeSpan?>();
                    List<RedisKey> keys = GetAllKeys(isThrowEx);
                    foreach (var key in keys)
                    {
                        try
                        {
                            keyExpireTime.Add(key, db.KeyTimeToLive(key));
                        }
                        catch (Exception ex)
                        {
                            LogError($"GetAllKeyTime Time ile tüm keylerin görüntülenmesinde {key} için hata aldı: {ex.Message}", ex);
                        }
                    }
                    return keyExpireTime;
                },
                "*",
                "GET_ALL_KEY_TIME", isThrowException: isThrowEx
            ) ?? new Dictionary<string, TimeSpan?>();
        }

        public TimeSpan? GetKeyTime(string key, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db => db.KeyTimeToLive(key),
                key,
                "GET_KEY_TIME", isThrowException: isThrowEx
            );
        }

        #endregion

        #region Exists and Conditional Operations

        public bool GetIfExists(string key, out string obj, bool isThrowEx = true)
        {
            obj = Get(key, isThrowEx);
            return (obj != null);
        }

        public bool GetIfExists<T>(string key, out T obj, bool isThrowEx = true) where T : class
        {
            obj = GetDeserializeBytes<T>(key, isThrowEx);
            if (obj == null)
            {
                DeleteByKey(key, isThrowEx: isThrowEx);
                return false;
            }
            return true;
        }

        public bool GetIfExistsWithExpiry<T>(string key, out Tuple<TimeSpan?, T> obj, bool isThrowEx = true) where T : class
        {
            obj = GetDeserializeBytesWithExpiry<T>(key, isThrowEx);
            if (obj == null || obj.Item2 == null)
            {
                DeleteByKey(key, isThrowEx: isThrowEx);
                return false;
            }
            return true;
        }

        public bool GetIfExistsObj(string key, out object obj, bool isThrowEx = true)
        {
            obj = Get(key, isThrowEx: isThrowEx);
            if (obj == null)
            {
                DeleteByKey(key, isThrowEx: isThrowEx);
                return false;
            }
            return true;
        }

        public bool ExistsLike(string key, bool isThrowEx = true)
        {
            var keys = GetAllKeysByLike(key, isThrowEx: isThrowEx);
            return keys?.Any() == true;
        }

        public bool ExistsLikeOld(string key, bool isThrowEx = true)
        {
            var keys = GetAllKeysByLikeOld(key, isThrowEx: isThrowEx);
            return keys?.Any() == true;
        }

        public bool ExistsPrefixAndLike(string prefix, List<string> subTextList, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    List<RedisKey> keys = GetAllKeysByLike(prefix);
                    var list = keys.Where(p => p.ToString().StartsWith(prefix)).Select(p => p.ToString().Replace(prefix, ""));
                    return list.Any(p => subTextList.Contains(p));
                },
                prefix,
                "EXISTS_PREFIX_AND_LIKE", isThrowException: isThrowEx
            );
        }

        public bool ExistsPrefixAndLikeOld(string prefix, List<string> subTextList, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    List<RedisKey> keys = GetAllKeys(isThrowEx);
                    var list = keys.Where(p => p.ToString().StartsWith(prefix)).Select(p => p.ToString().Replace(prefix, ""));
                    return list.Any(p => subTextList.Contains(p));
                },
                prefix,
                "EXISTS_PREFIX_AND_LIKE_OLD", isThrowException: isThrowEx
            );
        }

        #endregion

        #region Lock Operations

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
                    RedLockFactory = RedLockFactory.Create(new List<RedLockMultiplexer>() { _connectionMultiplexer as ConnectionMultiplexer });
                }

                return RedLockFactory;
            }
        }

        #endregion

        #region Serialization

        public byte[] Serialize<T>(T obj)
        {
            return MessagePack.MessagePackSerializer.Serialize<T>(obj);
        }

        public T Deserialize<T>(byte[] obj)
        {
            return MessagePack.MessagePackSerializer.Deserialize<T>(obj);
        }

        #endregion

        #region Container Lifecycle Management

        /// <summary>
        /// pod ölürken redis bağlantısını kapatır
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnContainerShutdown(object sender, EventArgs e)
        {
            try
            {
                LogInfo($"Pod {_containerPodName}: Kapatılıyor! redis aşamalı kapatma işlemi başladı!");

                if (_connectionMultiplexer?.IsConnected == true)
                {
                    var db = _connectionMultiplexer.GetDatabase();
                    db.KeyDelete($"container_registry:{_containerPodName}");
                }

                _connectionMultiplexer?.Close();
                _connectionMultiplexer?.Dispose();
                RedLockFactory?.Dispose();
            }
            catch (Exception ex)
            {
                LogError($"Pod shutdown hatası: {ex.Message}", ex);
            }
        }

        #endregion

        #region Connection Events

        private void RegisterConnectionEvents()
        {
            if (_connectionMultiplexer == null) return;

            _connectionMultiplexer.ConnectionRestored += (sender, e) =>
            {
                LogInfo($"Pod {_containerPodName}: Redis connection tekrar yapılandırıldı! {e.EndPoint}");
            };

            _connectionMultiplexer.ConnectionFailed += (sender, e) =>
            {
                LogError($"Pod {_containerPodName}: Redis bağlantı hatası! Endpoint: {e.EndPoint}: {e.Exception?.Message}");
            };

            _connectionMultiplexer.ErrorMessage += (sender, e) =>
            {
                LogError($"Pod {_containerPodName}: Redis hatası: {e.Message}");
            };

            _connectionMultiplexer.ConfigurationChanged += (sender, e) =>
            {
                LogInfo($"Pod {_containerPodName}: Redis ayarları değiştirildi: {e.EndPoint}");
            };

            _connectionMultiplexer.HashSlotMoved += (sender, e) =>
            {
                LogInfo($"Pod {_containerPodName}: Redis endpointi kaldırıldı! okuma yazma işlemlerini kontrol ediniz!: {e.OldEndPoint} to {e.NewEndPoint}");
            };

            _connectionMultiplexer.InternalError += (sender, e) =>
            {
                LogError($"Pod {_containerPodName}: Redis internal error: {e.Exception?.Message}");
            };

            _connectionMultiplexer.ConfigurationChangedBroadcast += (sender, e) =>
            {
                LogInfo($"Pod {_containerPodName}: Redis bağlantısının sağlandığı endpointin network ayarları değiştirildi!: {e.EndPoint}");
            };
        }

        #endregion

        #region Logging

        private void LogInfo(string message)
        {
            try
            {
                ElasticLogger.Instance.Info($"RedisManagerV2: {message}");
            }
            catch
            {
                Console.WriteLine($"[INFO] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - RedisManagerV2: {message}");
            }
        }

        private void LogError(string message, Exception ex = null)
        {
            try
            {
                ElasticLogger.Instance.Error(ex ?? new Exception("REDIS ERROR"), $"RedisManagerV2: {message}");
            }
            catch
            {
                Console.WriteLine($"[ERROR] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - RedisManagerV2: {message}");
            }
        }


        #endregion
    }
}