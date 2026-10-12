using System.Diagnostics;
using Xunit;

namespace DMicroservices.Redis.Compatibility;

public sealed class RedisIntegrationFactAttribute : FactAttribute
{
    public RedisIntegrationFactAttribute(bool failover = false)
    {
        if (Environment.GetEnvironmentVariable("REDIS_TEST_ENABLE") != "1")
            Skip = "Requires an explicit disposable Redis fixture (REDIS_TEST_ENABLE=1).";
        else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REDIS_URL")))
            Skip = "Requires REDIS_URL for the disposable fixture.";
        else if (failover && Environment.GetEnvironmentVariable("REDIS_TEST_ALLOW_FAILOVER") != "1")
            Skip = "Requires explicit REDIS_TEST_ALLOW_FAILOVER=1.";
    }
}

[CollectionDefinition("RedisCompatibility", DisableParallelization = true)]
public sealed class RedisCompatibilityCollection { }

[Collection("RedisCompatibility")]
public sealed class CompatibilityTests
{
    [RedisIntegrationFact]
    [Trait("Category", "RedisIntegration")]
    public Task StringHashTtlBloomScanDatabaseIsolationManagerAndLocks()
        => RunProbeAsync("--redis-probe", "PASS REDIS");

    [RedisIntegrationFact(true)]
    [Trait("Category", "SentinelFailover")]
    public Task ExistingConnectionsRecoverAfterSentinelFailover()
        => RunProbeAsync("--sentinel-probe", "PASS FAILOVER");

    private static async Task RunProbeAsync(string argument, string expected)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true
        };
        start.ArgumentList.Add(typeof(CompatibilityTests).Assembly.Location);
        start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Probe did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException("Redis probe timed out.\n" + await output + await error);
        }
        var text = await output;
        Assert.True(process.ExitCode == 0, text + await error);
        Assert.Contains("RUNTIME: 5.0.", text);
        Assert.Contains(expected, text);
    }
}
