using DMicroservices.DataAccess.Redis;
using StackExchange.Redis;

namespace DMicroservices.Redis.Compatibility;

internal static class RedisCompatibilityProbe
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static async Task RunAsync(bool failover)
    {
        string url = Environment.GetEnvironmentVariable("REDIS_URL");
        Check(!string.IsNullOrWhiteSpace(url), "REDIS_URL is required.");
        var options = ConfigurationOptions.Parse(url);
        Check(!failover || RedisConnectionFactory.IsSentinel(options), "Failover requires serviceName in REDIS_URL.");
        options.AbortOnConnectFail = false;
        options.AllowAdmin = true; // INFO, WAIT and the disposable Sentinel FAILOVER.
        options.ConnectTimeout = 15000;
        options.SyncTimeout = 5000;
        options.AsyncTimeout = 5000;
        using var redis = new RedisConnectionFactory().Connect(options);
        var database = redis.GetDatabase(0);
        string prefix = "compat:" + Guid.NewGuid().ToString("N") + ":";
        var expiry = TimeSpan.FromMinutes(5);
        Check(database.StringSet(prefix + "string", "value", expiry), "String set failed.");
        Check(database.StringGet(prefix + "string") == "value", "String get failed.");
        var ttl = database.KeyTimeToLive(prefix + "string");
        Check(ttl.HasValue && ttl > TimeSpan.Zero && ttl <= expiry, "TTL was not preserved.");
        database.HashSet(prefix + "hash", "field", "value");
        database.KeyExpire(prefix + "hash", expiry);
        Check(database.HashGet(prefix + "hash", "field") == "value", "Hash read failed.");
        database.Execute("BF.RESERVE", prefix + "bloom", 0.01, 100);
        database.KeyExpire(prefix + "bloom", expiry);
        database.Execute("BF.ADD", prefix + "bloom", "taxpayer");
        Check((int)database.Execute("BF.EXISTS", prefix + "bloom", "taxpayer") == 1, "RedisBloom failed.");
        var master = RedisConnectionFactory.GetMasterServer(redis);
        Check(master.IsConnected && !master.IsReplica, "Factory did not select the connected master.");
        Check(master.Keys(0, prefix + "*").Any(), "Master key scan failed.");

        var managerOptions = ConfigurationOptions.Parse(url);
        managerOptions.AbortOnConnectFail = false;
        managerOptions.ClientName = "compat-manager";
        managerOptions.ConnectRetry = 3;
        managerOptions.ConnectTimeout = 3000;
        managerOptions.SyncTimeout = 15000;
        managerOptions.AsyncTimeout = 15000;
        managerOptions.KeepAlive = 30;
        managerOptions.DefaultDatabase = 0;
        managerOptions.AllowAdmin = false;
        string originalOptions = managerOptions.ToString(includePassword: true);
        using (var managerConnection = new RedisConnectionFactory().Connect(managerOptions))
            Check(managerConnection.GetDatabase().StringSet(prefix + "manager-options", "value", expiry),
                "Manager-equivalent connection failed.");
        Check(managerOptions.ToString(includePassword: true) == originalOptions,
            "Factory mutated caller configuration.");

        var db15Options = managerOptions.Clone();
        db15Options.DefaultDatabase = 15;
        string originalDb15Options = db15Options.ToString(includePassword: true);
        using var db15Connection = new RedisConnectionFactory().Connect(db15Options);
        var db15 = db15Connection.GetDatabase();
        Check(db15.Database == 15 && db15Options.ToString(includePassword: true) == originalDb15Options,
            "Factory changed DB15 or caller options.");
        Check(db15.StringSet(prefix + "db15", "db15-value", expiry) &&
            db15.StringGet(prefix + "db15") == "db15-value" && !database.KeyExists(prefix + "db15"),
            "DB15 write/read or isolation from DB0 failed.");
        Console.WriteLine("PASS REDIS DATABASES: caller options and DB0/DB15 isolation preserved.");

        var manager = RedisManagerV2.Instance;
        Check(manager.Set(prefix + "manager", "manager-value", expiry), "RedisManagerV2 set failed.");
        Check(manager.Get(prefix + "manager") == "manager-value", "RedisManagerV2 get failed.");
        Check(RedLockManager.Instance.TryGetLockFactory(out var lockFactory), "Package RedLock factory failed.");
        using (var held = lockFactory.CreateLock(prefix + "lock", TimeSpan.FromSeconds(15)))
            Check(held.IsAcquired, "Package RedLock acquisition failed.");
        string version = master.Info("server").SelectMany(group => group)
            .First(pair => pair.Key == "redis_version").Value;
        Console.WriteLine("PASS REDIS: " + version + "; String/Hash/TTL/Bloom/SCAN/RedisManagerV2/RedLockManager.");

        if (!failover) return;
        Check((int)database.Execute("WAIT", 1, 5000) >= 1, "Replica was not ready for failover.");
        var sentinelOptions = options.Clone();
        sentinelOptions.User = null;
        sentinelOptions.Password = Environment.GetEnvironmentVariable("REDIS_SENTINEL_PASS");
        using var sentinel = ConnectionMultiplexer.SentinelConnect(sentinelOptions);
        string originalMaster = master.EndPoint.ToString();
        sentinel.GetServer(sentinel.GetEndPoints().First())
            .Execute("SENTINEL", "FAILOVER", options.ServiceName);
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(1000);
            try
            {
                var current = RedisConnectionFactory.GetMasterServer(redis);
                if (!current.IsConnected || current.IsReplica || current.EndPoint.ToString() == originalMaster) continue;
                if (!database.StringSet(prefix + "factory-after-failover", "recovered", expiry) ||
                    database.StringGet(prefix + "factory-after-failover") != "recovered") continue;
                if (!manager.Set(prefix + "manager-after-failover", "recovered", expiry) ||
                    manager.Get(prefix + "manager-after-failover") != "recovered") continue;
                if (!db15.StringSet(prefix + "db15-after-failover", "recovered", expiry) ||
                    db15.StringGet(prefix + "db15-after-failover") != "recovered" ||
                    database.KeyExists(prefix + "db15-after-failover")) continue;
                using var held = lockFactory.CreateLock(prefix + "lock-after-failover", TimeSpan.FromSeconds(10));
                if (!held.IsAcquired) continue;
                Console.WriteLine("PASS FAILOVER: master changed; existing factory, manager, DB15 and package lock connections recovered.");
                return;
            }
            catch (RedisException) { RedisConnectionFactory.ReconfigureAfterReadOnly(redis); }
            catch (RedisCircuitOpenException) { }
        }
        throw new TimeoutException("Sentinel failover did not recover writes and locks.");
    }
}
