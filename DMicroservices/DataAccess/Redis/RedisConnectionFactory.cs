using StackExchange.Redis;
using System;
using System.Linq;
using System.Threading;

namespace DMicroservices.DataAccess.Redis
{
    /// <summary>
    /// REDIS_URL'den redis connection nesnesini üretir.
    /// URL'de serviceName varsa (örn: "sentinel.gib.svc.cluster.local:5000,serviceName=mymaster,password=xxx") bağlantı Sentinel üzerinden kurulur,
    /// master Sentinel'e sorulur ve failover'da (+switch-master) yeni master'a geçilir.
    /// serviceName yoksa eskisi gibi verilen endpointlere (örn: "redis-0...:6379,redis-1...:6379,password=xxx") doğrudan bağlanılır.
    /// Sentinel modunda REDIS_URL'deki password Redis'in şifresidir; Sentinel'in kendi şifresi (requirepass) varsa REDIS_SENTINEL_PASS env'inden okunur.
    /// REDIS_URL ile kendi bağlantısını açan her yer ConnectionMultiplexer.Connect yerine bunu kullanmalı. Instance başına tek sentinel bağlantısı tutulur,
    /// </summary>
    public sealed class RedisConnectionFactory
    {
        // SE.Redis master/replica rollerini varsayılan olarak 60 saniyede bir kontrol eder.
        // Failover sonrası bağlantısı kopmayan eski master'a (artık replica) yazıp READONLY alınan süreyi kısaltmak için düşürüyoruz.
        private const int MaxConfigCheckSeconds = 5;

        // READONLY alındığında topolojiyi yeniden okutma isteğini her hatada değil, bu aralıkla en fazla bir kez yapıyoruz.
        private static readonly TimeSpan _reconfigureThrottle = TimeSpan.FromSeconds(5);
        private static long _lastReconfigureTicks;

        public const string SentinelPasswordVariable = "REDIS_SENTINEL_PASS";

        private readonly object _sentinelLock = new object();
        private ConnectionMultiplexer _sentinelConnection;

        public static bool IsSentinel(ConfigurationOptions options)
        {
            return !string.IsNullOrEmpty(options?.ServiceName);
        }

        public ConnectionMultiplexer Connect(ConfigurationOptions options)
        {
            var config = options.Clone();
            config.ConfigCheckSeconds = Math.Min(config.ConfigCheckSeconds, MaxConfigCheckSeconds);

            if (!IsSentinel(config))
                return ConnectionMultiplexer.Connect(config);

            // Sentinel'e ulaşılamaz veya master bulunamazsa RedisConnectionException fırlatır.
            // StackExchange.Redis 2.2.4 validates the Sentinel master with ROLE.
            config.AllowAdmin = true;
            return GetSentinelConnection(config).GetSentinelMasterConnection(config);
        }

        private ConnectionMultiplexer GetSentinelConnection(ConfigurationOptions config)
        {
            lock (_sentinelLock)
            {
               
                if (_sentinelConnection != null)
                    return _sentinelConnection;

                // REDIS_URL'deki password Redis'in şifresidir (sentinel.conf'taki "sentinel auth-pass" ile aynı), Sentinel'e gönderilmez.
                // Sentinel'in kendisine requirepass konursa şifresi REDIS_SENTINEL_PASS ile verilir; tanımlı değilse şifresiz bağlanılır.
                var sentinelConfig = config.Clone();
                sentinelConfig.User = null;
                string sentinelPassword = Environment.GetEnvironmentVariable(SentinelPasswordVariable);
                sentinelConfig.Password = string.IsNullOrEmpty(sentinelPassword) ? null : sentinelPassword;

                _sentinelConnection = ConnectionMultiplexer.SentinelConnect(sentinelConfig);
                return _sentinelConnection;
            }
        }

        /// <summary>
        /// SCAN/KEYS gibi sunucu bazlı komutlar için bağlı olan master sunucuyu döner.
        /// Replica seçilirse az önce yazılan keyler görünmeyebilir, bağlantısı kopmuş bir sunucu seçilirse istenen iş hata verebilir.
        /// </summary>
        public static IServer GetMasterServer(IConnectionMultiplexer connection)
        {
            var servers = connection.GetEndPoints().Select(p => connection.GetServer(p)).ToList();
            return servers.FirstOrDefault(p => p.IsConnected && !p.IsReplica)
                   ?? servers.FirstOrDefault(p => p.IsConnected)
                   ?? servers.First();
        }

        public static bool IsReadOnlyError(Exception ex)
        {
            return ex is RedisServerException && ex.Message.StartsWith("READONLY", StringComparison.Ordinal);
        }

        /// <summary>
        /// Failover sonrası eski mastera yazılıp READONLY alındığında, periyodik kontrolü beklemeden master/replica rollerini yeniden okutur.
        /// </summary>
        public static void ReconfigureAfterReadOnly(IConnectionMultiplexer connection)
        {
            long now = DateTime.UtcNow.Ticks;
            long last = Interlocked.Read(ref _lastReconfigureTicks);
            if (now - last < _reconfigureThrottle.Ticks || Interlocked.CompareExchange(ref _lastReconfigureTicks, now, last) != last)
                return;

            _ = connection.ConfigureAsync();
        }
    }
}
