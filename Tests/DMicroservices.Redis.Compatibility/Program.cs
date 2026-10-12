using DMicroservices.DataAccess.Redis;
using StackExchange.Redis;

namespace DMicroservices.Redis.Compatibility;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("RUNTIME: " + Environment.Version);
        if (args.Length != 1 || (args[0] != "--redis-probe" && args[0] != "--sentinel-probe"))
            throw new ArgumentException("Use --redis-probe or --sentinel-probe with REDIS_TEST_ENABLE=1.");
        if (Environment.Version.Major != 5)
            throw new InvalidOperationException("Compatibility must run on .NET 5.");
        if (Environment.GetEnvironmentVariable("REDIS_TEST_ENABLE") != "1")
            throw new InvalidOperationException("Explicit REDIS_TEST_ENABLE=1 is required.");
        bool failover = args[0] == "--sentinel-probe";
        if (failover && Environment.GetEnvironmentVariable("REDIS_TEST_ALLOW_FAILOVER") != "1")
            throw new InvalidOperationException("Sentinel FAILOVER requires REDIS_TEST_ALLOW_FAILOVER=1.");
        foreach (var key in new[] { "ELASTIC_URI", "ELASTIC_USERNAME", "ELASTIC_PASSWORD", "ELASTIC_URI_SSL" })
            Environment.SetEnvironmentVariable(key, null);
        await RedisCompatibilityProbe.RunAsync(failover);
    }
}
