using DMicroservices.Utils.Logger;
using MessagePack;
using MongoDB.Driver;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DMicroservices.DataAccess.Redis
{
    /// <summary>
    /// IsThrowEx parametresini dikkate alarak redis işlemleri yapınız. Bağımlılık gerekmiyorsa bilinçli olarak isThrowEx parametresini false işaretleyiniz ve cache yi kullanamadıgınız durumda db den işlem yapınız.
    /// Kullanılan her isThrowEx:true kalan işlemlerin gerekçeleriyle birlikte açıklanması gerekmektedir.
    /// </summary>
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
                            if (isThrowException) throw new RedisCircuitOpenException($"Redis aşırı bağlantı önleme sistemi aktif halde!. '{operationName}' emri çalıştırılmayacak.");
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
                        LogError($"Container {_containerPodName}: Redis bağlantısı başarısız! OperationName: {operationName}. Aşırı connection önleme sistemi devreye giriyor! {_circuitOpenDuration.TotalSeconds} saniye sürecek! Bu saniye boyunca redis connectionu açılmayacaktır.", ex);
                        _isCircuitOpen = true;
                        _lastCircuitOpenTime = DateTime.UtcNow;
                    }
                }

                if (isThrowException)
                    throw;
            }
            catch (Exception ex)
            {
                LogError($"Container {_containerPodName}: Redis '{key}' ile yapılan {operationName}  işlemi başarısız oldu! {ex.Message}", ex);
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
        /// <param name="key">Redisde kayıt edilen key</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya atsın mı ? True olduğunda dışarıya hatayı çıkarır, false olduğunda null dönüp sessizce devam eder</param>
        /// <returns>Db de o key de veri varsa string olarak döner, yoksa null döner, eğer isThrowEx false olur ise, db de key varsa bile hata durumunda sessizce devam et(isThrowEx false) olduğu için null döner</returns>
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
        /// Redise verilen key de string olarak veriyi kayıt eder
        /// </summary>
        /// <param name="key">Redis'e kayıt edilecek key</param>
        /// <param name="value">Redis'e verilen key'e karşılık gelecek kayıt edilecek value</param>
        /// <param name="expireTime">Verinin rediste ne kadar süre tutulacağı ?</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya atsın mı ? True olduğunda dışarıya hatayı çıkarır, false olduğunda dbye setlenmezse bile false dönüp sessizce devam eder</param>
        /// <returns>redise verilen key setlenirse true döner, setlenmezse false döner, isthrowEx false olupta setlenmezse false döner</returns>
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
        ///  Key'in varlığını kontrol eder. Redis yoksa false döner.
        /// </summary>
        /// <param name="key">Rediste aranacak key</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya atsın mı ? True olduğunda dışarıya hatayı çıkarır, false olduğunda veri varsa bile false dönüp sessizce devam eder</param>
        /// <returns>Key rediste varsa true, yoksa false, isThrowEx kapalı olupta veri olsa bile hata durumunda sessizce devam et denildiği için -> false döner</returns>
        public bool Exists(string key, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db => db.KeyExists(key),
                key,
                "EXISTS", isThrowException: isThrowEx
            );
        }

        /// <summary>
        /// verilen keyi redisten siler.
        /// </summary>
        /// <param name="key">Redisten silinecek key</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya atsın mı ? True olduğunda dışarıya hatayı çıkarır, false olduğunda veri db de varsa bile silmeden false dönüp sessizce devam eder</param>
        /// <returns>Key redisten silinirse true, silinmezse false, isThrowEx kapalı ise hata alıpta silinemezse bile false döner</returns>
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
        ///  Object'i serialize ederek Redis'e kaydeder.
        ///  DİKKAT! MessagePack da reference loop handling aktiftir, yani looplu bir nesne kayıt etmeye izin vermez! loop'lu bir nesne kayıt edecekseniz bilinçli olarak looplu nesneyi null'a çekin.
        /// </summary>
        /// <typeparam name="T">Kayıt edilecek nesnenin tipi</typeparam>
        /// <param name="key">Redis'e kayıt edilecek key</param>
        /// <param name="value">Redis'e verilen key ile kayıt edilen değişkenin değeri</param>
        /// <param name="expiry">Verinin rediste ne kadar süre tutulacağı ?</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya atsın mı ? True olduğunda dışarıya hatayı çıkarır, false olduğunda db ye set edemezse bile false dönüp sessizce devam eder</param>
        /// <returns>Redis'e setlenip setlenmediğinin bilgisi, isThrowEx kapalı ise setleme yapamasa bile false döner</returns>
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
        /// <typeparam name="T">Verilen key in dönüştürüleceği veri tipi</typeparam>
        /// <param name="key">Redisten getirilecek key</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya atsın mı ? True olduğunda dışarıya hatayı çıkarır, false olduğunda null dönüp sessizce devam eder</param>
        /// <returns>Verilen T typesine göre verilen keyin verisini deserialize ederek döner, eğer veri getirilemezse ve isThrowEx açıksa null döner ve sessizce devam eder</returns>
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

        /// <summary>
        /// Verilen Key'i verilen db numarasından arayarak getirir.
        /// </summary>
        /// <param name="key">rediste aranacak key</param>
        /// <param name="databaseNum">redisin hangi db sinden aranacaksa o db nin numarası</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya atsın mı ? True olduğunda dışarıya hatayı çıkarır, false olduğunda null dönüp sessizce devam eder</param>
        /// <returns>Db den istenen key'in value'sini döner, isThrowEx false olduğunda veri db de varsa bile hata durumunda throw atma dendiği için null döner</returns>
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

        /// <summary>
        /// Verilen key,value'yi redise verilen db numarasına göre set eder.
        /// </summary>
        /// <param name="key">Cache Key</param>
        /// <param name="value">Cache value</param>
        /// <param name="databaseNum">Cache db numarası</param>
        /// <param name="expireTime">Cachenin ne kadar süre db de kalacağı</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya atsın mı ? True olduğunda dışarıya hatayı çıkarır, false olduğunda dbye setlenmezse bile false dönüp sessizce devam eder</param>
        /// <returns>redise verilen key setlenirse true döner, setlenmezse false döner, isthrowEx false olupta setlenmezse false döner</returns>
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

        /// <summary>
        /// istenen keyi istenen db numarasıyla redisten siler
        /// </summary>
        /// <param name="key">silinecek key</param>
        /// <param name="databaseNum">silinecek db numarası</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya atsın mı ? True olduğunda dışarıya hatayı çıkarır, false olduğunda veri db de varsa bile silmeden false dönüp sessizce devam eder</param>
        /// <returns>Key redisten silinirse true, silinmezse false, isThrowEx kapalı ise hata alıpta silinemezse bile false döner</returns>
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

        /// <summary>
        /// Redis'e liste şeklinde veri setler, dictionary nin keyleri redis key, e valuesi ise value'e işaret eder, bulk bir setiniz varsa buradan setleyiniz.
        /// </summary>
        /// <param name="bulkInsertList">SEtlenecek bulk liste</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya atsın mı ? True olduğunda dışarıya hatayı çıkarır, false olduğunda dbye setlenmezse bile false dönüp sessizce devam eder</param>
        /// <returns>redise verilen dictionaryler setlenirse true döner, setlenmezse false döner, isthrowEx false olupta setlenmezse false döner</returns>
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

        /// <summary>
        /// Redis'e liste şeklinde veri setler, dictionary nin keyleri redis key, e valuesi ise value'e işaret eder, bulk bir setiniz varsa buradan setleyiniz.
        /// </summary>
        /// <param name="bulkInsertList">SEtlenecek bulk liste</param>
        /// <param name="databaseNum">Cache db numarası</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya atsın mı ? True olduğunda dışarıya hatayı çıkarır, false olduğunda dbye setlenmezse bile false dönüp sessizce devam eder</param>
        /// <returns>redise verilen dictionaryler setlenirse true döner, setlenmezse false döner, isthrowEx false olupta setlenmezse false döner</returns>
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

        /// <summary>
        /// Redisten istenen keydeki veriyi kalan zamanıyla birlikte getirir
        /// </summary>
        /// <param name="key">Rediste aranacak key</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya atsın mı ? True olduğunda dışarıya hatayı çıkarır, false olduğunda null dönüp sessizce devam eder</param>
        /// <returns>Db de o key de veri varsa string olarak döner, yoksa null döner, eğer isThrowEx false olur ise, db de key varsa bile hata durumunda sessizce devam et(isThrowEx false) olduğu için null döner</returns>
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

        /// <summary>
        /// Redisten db numarasına göre istenen keydeki veriyi kalan zamanıyla birlikte getirir
        /// </summary>
        /// <param name="key">Rediste aranacak key</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya atsın mı ? True olduğunda dışarıya hatayı çıkarır, false olduğunda null dönüp sessizce devam eder</param>
        /// <returns>Db de o key de veri varsa string olarak döner, yoksa null döner, eğer isThrowEx false olur ise, db de key varsa bile hata durumunda sessizce devam et(isThrowEx false) olduğu için null döner</returns>
        public Tuple<TimeSpan?, string> GetWithExpiry(string key, int databaseNum, bool isThrowEx = true)
        {
            return ExecuteRedisOperation(
                db =>
                {
                    var cacheValue = db.StringGetWithExpiry(key);
                    return new Tuple<TimeSpan?, string>(cacheValue.Expiry, cacheValue.Value.HasValue ? cacheValue.Value.ToString() : null);
                },
                key,
                "GET_WITH_EXPIRY", databaseNum, isThrowException: isThrowEx
            );
        }

        /// <summary>
        /// Object'i deserialize ederek Redis'ten kalan zamanıyla birlikte getirir.
        /// </summary>
        /// <typeparam name="T">Verilen key in dönüştürüleceği veri tipi</typeparam>
        /// <param name="key">Redisten getirilecek key</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya atsın mı ? True olduğunda dışarıya hatayı çıkarır, false olduğunda null dönüp sessizce devam eder</param>
        /// <returns>Verilen T typesine göre verilen keyin verisini deserialize ederek Tuple<Timespan?,T> şeklinde döner Item1 -> kalan zamanı TimeSpan nesnesinde döner, Item2 -> getirilecek verinin kendisi, eğer veri getirilemezse ve isThrowEx açıksa null döner ve sessizce devam eder</returns>
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

        /// <summary>
        /// Redisteki verilen databaseNum'a göre olan tüm keyleri getirir
        /// </summary>
        /// <param name="databaseNum">Cache db numarası</param>
        /// <param name="pageSize">Redis db den verilerin iç tarafta kaçar kaçar sorgulanacağının sayısıdır, yüksek bir rediskey setlemesi var ise sayıyı 250 nin aşağısına çekiniz.</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya atsın mı ? True olduğunda dışarıya hatayı çıkarır, false olduğunda empty list döner sessizce devam eder</param>
        /// <returns>Verilen redis db numarasındaki tüm keyler dönülür. eğer isThrowEx false ise, hata aldığında db de veri olsa bile boş liste döner.</returns>
        public IEnumerable<RedisKey> ScanAllKey(int databaseNum = -1, int pageSize = 250, bool isThrowEx = true)
        {
            return ScanKeysByPattern("*", databaseNum, pageSize, isThrowEx: isThrowEx);
        }

        #region Scan Pattern operasyonları

        private IEnumerable<RedisKey> ScanKeysByPattern(string searchKey, int databaseNum = -1, int pageSize = 250, bool isThrowEx = true)
        {
            lock (_circuitLock)
            {
                if (_isCircuitOpen)
                {
                    if (DateTime.UtcNow - _lastCircuitOpenTime < _circuitOpenDuration)
                    {
                        if (isThrowEx) throw new RedisCircuitOpenException($"Redis aşırı bağlantı koruması açık!. ScanKeysByPattern için aradığınız  '{searchKey}' değer 30 saniye boyunca getirilmeyecek.");
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
                if (isThrowEx) throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis connectionu elde edilemedi!");
                return Enumerable.Empty<RedisKey>();
            }

            var server = connection.GetServer(connection.GetEndPoints().First());
            return ScanKeysImplementation(server, searchKey, databaseNum, pageSize, isThrowEx);
        }

        private IEnumerable<RedisKey> ScanKeysImplementation(IServer server, string searchKey, int databaseNum, int pageSize, bool isThrowEx)
        {
            IEnumerator<RedisKey> enumerator = null;
            try
            {
                enumerator = server.Keys(databaseNum, searchKey, pageSize).GetEnumerator();
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
                                _isCircuitOpen = true;
                                _lastCircuitOpenTime = DateTime.UtcNow;
                            }
                        }

                        if (isThrowEx)
                        {
                            throw new Exception("Redis verileri scan ederken hata aldı! connection veya timeout almış olabilir.", ex);
                        }

                        yield break;
                    }

                    if (hasNext)
                    {
                        if (_isCircuitOpen)
                        {
                            lock (_circuitLock) { if (_isCircuitOpen) _isCircuitOpen = false; }
                        }
                        LogInfo("Redis connection tekrar açıldı, bağlantı koruması devre dışı bırakıldı!");

                        yield return enumerator.Current;
                    }
                }
            }
            finally
            {
                enumerator?.Dispose();
            }
        }

        /// <summary>
        /// Verilen database numarasındaki tüm keyleri siler
        /// </summary>
        /// <param name="databaseNum">Redis Db numarası</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya atsın mı ? True olduğunda dışarıya hatayı çıkarır, false olduğunda false dönüp sessizce devam eder</param>
        /// <returns>Keyler redisten silinirse true, silinmezse false, isThrowEx kapalı ise hata alıpta silinemezse bile false döner</returns>
        public bool Clear(int databaseNum = -1, bool isThrowEx = true)
        {
            return DeleteByPattern("*", databaseNum, isThrowEx: isThrowEx) > 0;
        }

        /// <summary>
        /// verilen redis database numarasına göre, searchKeyText olarak verilen değeri contains(*searchKeyText*) ederek arar. 
        /// </summary>
        /// <param name="searchKeyText">rediste aranacak key</param>
        /// <param name="databaseNum">redisin hangi db sinde aranacağı</param>
        /// <param name="pageSize">arama yaparken kaçar kaçar arama yapacak ? büyük bir veri araması yapacağınızın öngörüsü varsa bu değeri 250 nin altına düşürün, örneğin 3000 key içerisinde bu vereceğiniz *searchKeyText* değerinin olabileceğini düşünüyorsanız 250 in altına düşürmelisiniz ve performans değerlendirmesi yapmalısınız</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya atsın mı ? True olduğunda dışarıya hatayı çıkarır, false olduğunda boş liste dönüp sessizce devam eder</param>
        /// <returns>verilen searchKeyText e uyan rediskeyleri döner</returns>
        public IEnumerable<RedisKey> ScanKeysContaining(string searchKeyText, int databaseNum = -1, int pageSize = 250, bool isThrowEx = true)
        {
            return ScanKeysByPattern($"*{searchKeyText}*", databaseNum, pageSize, isThrowEx: isThrowEx);
        }

        /// <summary>
        /// verilen redis database numarasına göre, searchKeyText olarak verilen değeri startwith(searchKeyText*) ederek arar. 
        /// </summary>
        /// <param name="searchKeyText">rediste aranacak key</param>
        /// <param name="databaseNum">redisin hangi db sinde aranacağı</param>
        /// <param name="pageSize">arama yaparken kaçar kaçar arama yapacak ? büyük bir veri araması yapacağınızın öngörüsü varsa bu değeri 250 nin altına düşürün, örneğin 3000 key içerisinde bu vereceğiniz searchKeyText* değerinin olabileceğini düşünüyorsanız 250 in altına düşürmelisiniz ve performans değerlendirmesi yapmalısınız</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya atsın mı ? True olduğunda dışarıya hatayı çıkarır, false olduğunda boş liste dönüp sessizce devam eder</param>
        /// <returns>verilen searchKeyText ile başlayan rediskeyleri döner</returns>

        public IEnumerable<RedisKey> ScanKeysStartingWith(string prefix, int databaseNum = -1, int pageSize = 250, bool isThrowEx = true)
        {
            return ScanKeysByPattern($"{prefix}*", databaseNum, pageSize, isThrowEx: isThrowEx);
        }

        /// <summary>
        /// todo: 'user:????:profile' karmaşık denenler içindir, ihtyiyaç olursa public yapalım.
        /// </summary>
        private IEnumerable<RedisKey> ScanKeysWithAdvancedPattern(string rawRedisPattern, int databaseNum = -1, int pageSize = 250, bool isThrowEx = true)
        {
            return ScanKeysByPattern(rawRedisPattern, databaseNum, pageSize, isThrowEx);
        }

        /// <summary>
        /// Verilen desene uyan tüm keyleri Redis'ten siler.
        /// </summary>
        /// <param name="searchKey">Silinecek key yapisi, default olarak contains ederek siler örneğin (*LockItems*) </param>
        /// <param name="databaseNum">Db numarası</param>
        /// <param name="scanPageSize">Redisten silinmek üzere kaçar kaçar veri getirileceğinin sayısı</param>
        /// <param name="deleteBatchSize">Db den kaçar kaçar silinme yapılacağının sayısı</param>
        /// <param name="isStartWithControl">True ise 'pattern*' StartWith ile, false ise '*pattern*' contains şeklinde arama yapılır</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya atsın mı ? True olduğunda dışarıya hatayı çıkarır, false olduğunda 0 dönüp sessizce devam eder</param>
        /// <returns>Silinen toplam sayıyı döner</returns>
        public long DeleteByPattern(string searchKey, int databaseNum = -1, int scanPageSize = 250, int deleteBatchSize = 1000, bool isStartWithControl = false, bool isThrowEx = true)
        {
            lock (_circuitLock)
            {
                if (_isCircuitOpen)
                {
                    if (DateTime.UtcNow - _lastCircuitOpenTime < _circuitOpenDuration)
                    {
                        if (isThrowEx) throw new RedisCircuitOpenException($"Redis aşırı bağlantı koruması açık!. DeleteByPattern için aradığınız  '{searchKey}' değer 30 saniye boyunca getirilmeyecek.");
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
                    throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis connectionu açılamadı");
                }

                var server = connection.GetServer(connection.GetEndPoints().First());
                var database = connection.GetDatabase(databaseNum);

                string scanPattern = isStartWithControl ? $"{searchKey}*" : $"*{searchKey}*";

                var keysToProcess = ScanKeysByPattern(scanPattern, databaseNum, scanPageSize, isThrowEx);

                var keysInChunk = new List<RedisKey>(deleteBatchSize);
                foreach (var key in keysToProcess)
                {
                    keysInChunk.Add(key);

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
                            LogInfo("Redis connection tekrar açıldı, bağlantı koruması devre dışı bırakıldı!");
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is RedisConnectionException || ex is RedisTimeoutException || ex is RedisCircuitOpenException)
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
                if (isThrowEx) throw;
            }
            catch (Exception ex)
            {
                LogError($"DeleteByPattern metohdunda, beklenmeyen hata!  '{searchKey}'.", ex);
                if (isThrowEx) throw;
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

        #endregion

        #region Exists and Conditional Operations

        /// <summary>
        /// Verilen key'in Redis'te var olup olmadığını kontrol eder ve varsa değerini getirir.
        /// </summary>
        /// <param name="key">Redis'te aranacak key</param>
        /// <param name="obj">Key bulunursa değeri bu parametreye setlenir, bulunamazsa null döner</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya exception atıp atmayacağı</param>
        /// <returns>Key bulunursa true, bulunamazsa false döner</retu
        public bool GetIfExists(string key, out string obj, bool isThrowEx = true)
        {
            obj = Get(key, isThrowEx);
            return (obj != null);
        }

        /// <summary>
        /// Verilen key'in Redis'te var olup olmadığını kontrol eder ve varsa belirtilen tipe convert ederek getirir.
        /// Key bulunamazsa veya dönüştürülemezse redisten silinir.
        /// </summary>
        /// <typeparam name="T">Dönüştürülecek nesnenin tipi</typeparam>
        /// <param name="key">Redis'te aranacak key</param>
        /// <param name="obj">Key bulunursa ve dönüştürülebilirse bu parametreye setlenir, bulunamazsa null setlenir</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya exception atıp atmayacağı?</param>
        /// <returns>Key bulunur ve convert edilebilirse true, bulunamaz veya convert edilemezse false</returns>

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

        /// <summary>
        /// Verilen key'in Redis'te var olup olmadığını kontrol eder, varsa kalan süre bilgisiyle birlikte belirtilen tipe convert ederek getirir.
        /// Key bulunamazsa veya dönüştürülemezse Redis'ten silinir.
        /// </summary>
        /// <typeparam name="T">Dönüştürülecek nesnenin tipi</typeparam>
        /// <param name="key">Redis'te aranacak key</param>
        /// <param name="obj">Key bulunursa ve dönüştürülebilirse Tuple olarak (süre bilgisi, nesne) şeklinde bu parametreye setlenir</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya exception  atıp atmayacağı ?</param>
        /// <returns>Key bulunur ve convert edilebilirse true, bulunamaz veya convert edilemezse false</returns>

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

        /// <summary>
        /// Verilen key'in Redis'te var olup olmadığını kontrol eder ve varsa object tipinde getirir.
        /// Key bulunamazsa Redis'ten silinir.
        /// </summary>
        /// <param name="key">Redis'te aranacak key</param>
        /// <param name="obj">Key bulunursa bu parametreye setlenir, bulunamazsa null setlenir</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya exception atıp atmayacağı</param>
        /// <returns>Key bulunursa true, bulunamazsa false döner</returns>
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

        /// <summary>
        /// Verilen desene uyan herhangi bir key'in Redis'te var olup olmadığını kontrol eder.
        /// default olarak *KEY* CONTAINS olarak arar, isStartWithControl açılırsa StartWith olarak arar, içeride aranan veri sayısına göre performans sorunları oluşturabilir, bu yüzden dikkatli kullanılmalıdır.
        /// </summary>
        /// <param name="searchKey">Aranacak veri stringi</param>
        /// <param name="databaseNum">Hangi Redis veritabanında arama yapılacağı</param>
        /// <param name="pageSize">Redisten kaçar kaçar arama yapacağı</param>
        /// <param name="isThrowEx">Hata durumunda dışarıya exception fırlatılsın mı?</param>
        /// <param name="isStartWithControl">True ise searchKey ile başlayan keyler için, false ise searchKey içeren keyler için arama yapar</param>
        /// <returns>searchKey'e uyan herhangi bir key bulunursa true, bulunamazsa false döner</returns>
        public bool ExistsByPattern(string searchKey, int databaseNum = -1, int pageSize = 250, bool isThrowEx = true, bool isStartWithControl = false)
        {
            try
            {
                if (isStartWithControl)
                    return ScanKeysStartingWith(searchKey, databaseNum, pageSize: pageSize, isThrowEx: isThrowEx).Any();
                return ScanKeysContaining(searchKey, databaseNum, pageSize: pageSize, isThrowEx: isThrowEx).Any();
            }
            catch (Exception ex)
            {
                LogError($"ExistsByPattern methodunda Beklenmeyen hata '{searchKey}'.", ex);
                return false;
            }
        }

        [Obsolete("Redis sunucusunun cevap vermemesine sebep olmaktadır!(Thread thief/connection timeout vb.) Kullanımdan kaldırıp ExistsByPattern methoduna geçiş yapınız!")]
        public bool ExistsLike(string key, bool isThrowEx = true)
        {
            var keys = GetAllKeysByLike(key, isThrowEx: isThrowEx);
            return keys?.Any() == true;
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