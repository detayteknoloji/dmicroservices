using DMicroservices.Utils.Logger;
using MessagePack;
using MongoDB.Driver;
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

        // redis down oldugunda circuit breaker kaç saniye işlemlere yok çeksin ? throw için yine circuit breaker devre dışıdır throw atar.
        private static readonly object _circuitLock = new object();
        private static volatile bool _isCircuitOpen = false;
        private static DateTime _lastCircuitOpenTime = DateTime.MinValue;
        private static readonly TimeSpan _circuitOpenDuration = TimeSpan.FromSeconds(30); // 30 saniye boyunca denemeye izin vermciez.


        #region Singleton section
        private static readonly Lazy<RedisManagerV2> _instance =
            new Lazy<RedisManagerV2>(() => new RedisManagerV2());

        private RedisManagerV2()
        {
            LogInfo($"Container {_containerPodName}: Initializing Redis ManagerV2.");

            MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard.WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);

            AppDomain.CurrentDomain.ProcessExit += OnContainerShutdown;
            Console.CancelKeyPress += (s, e) => OnContainerShutdown(s, null);
        }

        public static RedisManagerV2 Instance => _instance.Value;
        #endregion

        #region Bağlantı ayarlamaları ve retry denemeleri

        // connection dog
        private IConnectionMultiplexer GetConnection()
        {
            if (_connectionMultiplexer != null && _connectionMultiplexer.IsConnected)
            {
                return _connectionMultiplexer;
            }

            lock (_connectionLock)
            {
                if (_connectionMultiplexer != null && _connectionMultiplexer.IsConnected)
                {
                    return _connectionMultiplexer;
                }

                _connectionMultiplexer?.Dispose();

                if (string.IsNullOrEmpty(_redisUrl))
                {
                    LogInfo($"Container {_containerPodName}: Redis url bilgisi bulunamadı!. Lock ve cache işlemleri gerçekleşmeyecek!");
                    return null;
                }

                try
                {
                    LogInfo("Redis Bağlantısı kurmak istenildi, yeni connection açılmak veya eski connection ezilmek züere multiplexer acilacak");
                    var options = ConfigurationOptions.Parse(_redisUrl);
                    options.AbortOnConnectFail = false;
                    options.ClientName = $"Container-{_containerPodName}";
                    options.AbortOnConnectFail = false;
                    options.ConnectRetry = 3;
                    options.ConnectTimeout = 3000;  // 3 saniye içinde hankshkae yapmalı
                    options.SyncTimeout = 15000;     // 3 saniyede gerekli cevabı getirmeli // şimdilik 15 saniyede kalsın, obsolote methodlar kalkınca 3 saniyeye düşürmeli
                    options.AsyncTimeout = 15000;    // 3 saniyede gerekli cevabı getirmeli // şimdilik 15 saniyede kalsın, obsolote methodlar kalkınca 3 saniyeye düşürmeli
                    options.KeepAlive = 30;         // 30 saniyeyede bir connectionu ben açığım diye bildirim atmalı

                    options.DefaultDatabase = 0; // default 0 da olsun.

                    _connectionMultiplexer = ConnectionMultiplexer.Connect(options);
                    RegisterConnectionEvents();
                    LogInfo($"Container {_containerPodName}: Redis bağlantısı sağlandı");
                }
                catch (Exception ex)
                {
                    LogError("Yeni redis connectionu yaratılırken hata alındı", ex);
                    _connectionMultiplexer = null;
                }
            }
            return _connectionMultiplexer;
        }

        private IDatabase GetDatabase(int dataBaseNum = -1)
        {
            var connection = GetConnection();
            if (connection == null) // dog connectionu getirmediyse connection problemlidir circut ettirelim
            {
                throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis connection elde edilemedi!");
            }

            return connection.GetDatabase(dataBaseNum);
        }

        #endregion

        #region Core Operation - Tüm operasyonlar buradan yönetilir.

        private T ExecuteRedisOperation<T>(Func<IDatabase, T> redisOperation, string key, string operationName, int databaseNum = -1, bool isThrowException = false)
        {

            if (string.IsNullOrWhiteSpace(key))
            {
                return default(T);
            }

            try
            {
                lock (_circuitLock)
                {
                    if (_isCircuitOpen)
                    {
                        if (DateTime.UtcNow - _lastCircuitOpenTime < _circuitOpenDuration)
                        {
                            if (isThrowException) throw new RedisCircuitOpenException($"Redis circuit is open. Operation '{operationName}' was not attempted.");
                            return default;
                        }

                        _lastCircuitOpenTime = DateTime.UtcNow;
                    }
                }

                var database = GetDatabase(databaseNum);
                if (database != null)
                {
                    var result = redisOperation(database);
                    if (_isCircuitOpen)
                    {
                        lock (_circuitLock)
                        {
                            if (_isCircuitOpen)
                            {
                                _isCircuitOpen = false;
                                LogInfo("Redis bağlantısı tekrar sağlandı. Redis connection önleme sistemi kapatıldı!");
                            }
                        }
                    }
                    return result;
                }
            }
            catch (Exception ex) when (ex is RedisConnectionException || ex is RedisTimeoutException)
            {
                lock (_circuitLock)
                {
                    if (!_isCircuitOpen)
                    {
                        LogError($"Container {_containerPodName}: Redis bağlantısı başarısız! OperationName: {operationName}. Connection önleme sistemi devreye giriyor! {_circuitOpenDuration.TotalSeconds} saniye sürecek! Bu saniye boyunca redis connectionu açılmayacaktır.", ex);
                        _isCircuitOpen = true;
                        _lastCircuitOpenTime = DateTime.UtcNow;
                    }
                }

                if (isThrowException)
                    throw;
            }
            catch (Exception ex)
            {
                LogError($"Container {_containerPodName}: Redis '{key}' ile yapılan {operationName}  işemi başarısız oldu! {ex.Message}", ex);
                if (isThrowException)
                    throw;
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
            return ExecuteRedisOperation(
                                         db =>
                                         {
                                             var result = db.StringGet(key);
                                             return result.HasValue ? result.ToString() : null;
                                         },
                                         key,
                                         "GET",
                                         databaseNum,
                                         isThrowEx
                                     );

        }

        public bool Set(string key, string value, int databaseNum, TimeSpan? expireTime = null, bool isThrowEx = true)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                return false;

            return ExecuteRedisOperation(
                db => db.StringSet(key, value, expireTime),
                key,
                "SET",
                databaseNum,
                isThrowEx
            );
        }

        public bool DeleteByKey(string key, int databaseNum, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
           db => db.KeyDelete(key),
           key,
           "SET",
           databaseNum,
           isThrowEx);
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
            var keyValuePair = bulkInsertList.Select(x => new KeyValuePair<RedisKey, RedisValue>(x.Key, x.Value)).ToArray();
            string logKey = $"BULK_{keyValuePair.Length}_ITEMS";

            return ExecuteRedisOperation(
                db => db.StringSet(keyValuePair),
                logKey,
                "BULK_SET",
                databaseNum,
                isThrowEx
            );
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

        [Obsolete("Redis sunucusunun cevap vermemesine sebep olmaktadır! Kullanımdan kaldırıp ScanKeysByPattern methoduna geçiş yapınız!")]
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

        #region Scan Pattern operasyonları

        public IEnumerable<RedisKey> ScanKeysByPattern(string pattern, int databaseNum = -1, int pageSize = 250)
        {
            lock (_circuitLock)
            {
                if (_isCircuitOpen)
                {
                    if (DateTime.UtcNow - _lastCircuitOpenTime < _circuitOpenDuration)
                    {
                        return Enumerable.Empty<RedisKey>();
                    }
                    _lastCircuitOpenTime = DateTime.UtcNow;
                }
            }

            var connection = GetConnection();
            if (connection == null)
            {
                lock (_circuitLock)
                {
                    if (!_isCircuitOpen)
                    {
                        _isCircuitOpen = true;
                        _lastCircuitOpenTime = DateTime.UtcNow;
                    }
                }
                return Enumerable.Empty<RedisKey>();
            }

            var server = connection.GetServer(connection.GetEndPoints().First());
            return ScanKeysImplementation(server, pattern, databaseNum, pageSize);
        }

        private IEnumerable<RedisKey> ScanKeysImplementation(IServer server, string pattern, int databaseNum, int pageSize)
        {
            IEnumerator<RedisKey> enumerator = null;
            try
            {
                enumerator = server.Keys(databaseNum, $"*{pattern}*", pageSize).GetEnumerator();
                bool hasNext = true;

                while (hasNext)
                {
                    try
                    {
                        hasNext = enumerator.MoveNext();
                    }
                    catch (Exception ex) when (ex is RedisConnectionException || ex is RedisTimeoutException)
                    {
                        lock (_circuitLock)
                        {
                            if (!_isCircuitOpen)
                            {
                                LogError("Redis key scan yapılırken bağlantı koptu, bağlantı koruması devreye alınıyor!", ex);
                                _isCircuitOpen = true;
                                _lastCircuitOpenTime = DateTime.UtcNow;
                            }
                        }
                        yield break;
                    }

                    if (hasNext)
                    {
                        if (_isCircuitOpen)
                        {
                            lock (_circuitLock)
                            {
                                if (_isCircuitOpen)
                                    _isCircuitOpen = false;
                            }
                            LogInfo("Redis connection tekrar açıldı, bağlantı koruması devre dışı bırakıldı!");
                        }
                        yield return enumerator.Current;
                    }
                }
            }
            finally
            {
                enumerator?.Dispose();
            }
        }

        public bool Clear(int databaseNum = -1, bool isThrowEx = true)
        {
            var keys = ScanKeysByPattern("*", databaseNum);
            if (!keys.Any()) return true;

            return DeleteByPattern("*", databaseNum) > 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pattern">* olarak direkt girmeyiniz,redis cevap vermeyı bırakabilir</param>
        /// <param name="databaseNum">kaçıncı db?</param>
        /// <param name="scanPageSize">kaçar kaçar db den getirsin</param>
        /// <param name="deleteBatchSize">kaç belgede bir silmeye gitsin? (örneğin 2000 belge var, 250 250 getirir, 1000 oldugunda 1000 taneyi gidip silme emri verir.)</param>
        /// <returns></returns>
        public long DeleteByPattern(string pattern, int databaseNum = -1, int scanPageSize = 250, int deleteBatchSize = 1000, bool isStartWithControl = false)
        {
            lock (_circuitLock)
            {
                if (_isCircuitOpen)
                {
                    if (DateTime.UtcNow - _lastCircuitOpenTime < _circuitOpenDuration)
                    {
                        return 0;
                    }
                    else
                    {
                        _lastCircuitOpenTime = DateTime.UtcNow;
                    }
                }
            }

            long totalDeletedCount = 0;
            try
            {
                var connection = GetConnection();
                if (connection == null)
                {
                    throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "RedisConnectionu alınamadı!");
                }

                var server = connection.GetServer(connection.GetEndPoints().First());
                var database = connection.GetDatabase(databaseNum);

                var keysInChunk = new List<RedisKey>(deleteBatchSize);
                foreach (var key in server.Keys(databaseNum, $"*{pattern}*", pageSize: scanPageSize))
                {
                    if (isStartWithControl)
                    {
                        if (key.ToString().StartsWith(pattern))
                        {
                            keysInChunk.Add(key);
                        }
                    }
                    else
                    {
                        keysInChunk.Add(key);
                    }

                    if (keysInChunk.Count >= deleteBatchSize)
                    {
                        database.KeyDelete(keysInChunk.ToArray());
                        totalDeletedCount += keysInChunk.Count;
                        keysInChunk.Clear();
                    }
                }

                if (keysInChunk.Count > 0)
                {
                    database.KeyDelete(keysInChunk.ToArray());
                    totalDeletedCount += keysInChunk.Count;
                }

                if (_isCircuitOpen)
                {
                    lock (_circuitLock)
                    {
                        if (_isCircuitOpen)
                        {
                            _isCircuitOpen = false;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is RedisConnectionException || ex is RedisTimeoutException)
            {
                lock (_circuitLock)
                {
                    if (!_isCircuitOpen)
                    {
                        LogError($"Redis silme yaparken hata aldı! Bağlantı koruma devreye girecek!", ex);
                        _isCircuitOpen = true;
                        _lastCircuitOpenTime = DateTime.UtcNow;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"DeleteByPattern methodu çalışırken hata aldı! silme patterni: '{pattern}'.", ex);
            }

            return totalDeletedCount;
        }

        #endregion

        [Obsolete("Redis sunucusunun cevap vermemesine sebep olmaktadır! Kullanımdan kaldırıp ScanKeysByPattern methoduna geçiş yapınız!")]
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

        [Obsolete("Redis sunucusunun cevap vermemesine sebep olmaktadır! Kullanımdan kaldırıp ScanKeysByPattern methoduna geçiş yapınız!")]
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

        [Obsolete("Redis sunucusunun cevap vermemesine sebep olmaktadır! Kullanımdan kaldırıp ScanKeysByPattern methoduna geçiş yapınız!")]
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

        [Obsolete("Redis sunucusunun cevap vermemesine sebep olmaktadır! Kullanımdan kaldırıp ScanKeysByPattern methoduna geçiş yapınız!")]
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
                -1,
                isThrowEx
            );
        }

        [Obsolete("Redis sunucusunun cevap vermemesine sebep olmaktadır! Kullanımdan kaldırıp DeleteByPattern methoduna geçiş yapınız!")]
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
                "DELETE_BY_LIKE_OLD", -1, isThrowEx
            );
        }

        [Obsolete("Redis sunucusunun cevap vermemesine sebep olmaktadır! Kullanımdan kaldırıp DeleteByPattern methoduna geçiş yapınız!")]
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
                 -1, isThrowEx
            );
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

        [Obsolete("Redis sunucusunun cevap vermemesine sebep olmaktadır!(Thread thief/connection timeout vb.) Kullanımdan kaldırıp ExistsByPattern methoduna geçiş yapınız!")]
        public bool ExistsLike(string key, bool isThrowEx = true)
        {
            var keys = GetAllKeysByLike(key, isThrowEx: isThrowEx);
            return keys?.Any() == true;
        }

        public bool ExistsByPattern(string pattern, int databaseNum = -1)
        {
            try
            {
                return ScanKeysByPattern(pattern, databaseNum).Any();
            }
            catch (Exception ex)
            {
                LogError($"An error occurred in ExistsByPattern for pattern '{pattern}'.", ex);
                return false;
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
                LogInfo($"Pod {_containerPodName}: Kapatılıyor!");

                _connectionMultiplexer?.Close();
                _connectionMultiplexer?.Dispose();
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