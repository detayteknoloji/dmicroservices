using DMicroservices.Elastic.TestSupport;
using Xunit;

namespace DMicroservices.Elastic.Smoke;

public class SmokeTests
{
    private static Dictionary<string, string> Settings(ElasticHttpFixture fixture, string route = "serilog-{0:yyyy.MM}") => new()
    {
        ["ELASTIC_URI"] = fixture.Address,
        ["IS_FILE_LOG"] = "false",
        ["ELASTIC_BOOTSTRAP_METHOD"] = "None",
        ["LOG_INDEX_FORMAT"] = route,
        ["SMOKE_VERIFY_TIMEOUT_SECONDS"] = "3",
        ["SMOKE_TEST_ID"] = "reused-smoke-id"
    };

    [Fact]
    [Trait("Category", "LocalHttpFixture")]
    public async Task RoutesUseTypelessBulkAndCurrentRunMustBeDelivered()
    {
        await using var fixture = await ElasticHttpFixture.StartAsync();
        var settings = Settings(fixture);
        var accepted = await ProbeRunner.RunAsync(typeof(SmokeTests).Assembly, settings);
        Assert.True(accepted.ExitCode == 0, accepted.Output + accepted.Error);
        Assert.Equal(3, fixture.Records.Length);
        string month = DateTime.UtcNow.ToString("yyyy.MM", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(new[] { "error-serilog-" + month, "info-ledms-rabbitmq", "info-serilog-" + month },
            fixture.Records.Select(record => record.Index).OrderBy(index => index, StringComparer.Ordinal));
        foreach (var record in fixture.Records)
        {
            Assert.True(record.Document.TryGetProperty("@timestamp", out _));
            Assert.True(record.Document.TryGetProperty("messageTemplate", out _));
            Assert.True(record.Document.TryGetProperty("fields", out _));
        }
        fixture.RejectItems = true;
        var rejected = await ProbeRunner.RunAsync(typeof(SmokeTests).Assembly, settings);
        Assert.NotEqual(0, rejected.ExitCode);
        Assert.Contains("not delivered", rejected.Error);
        Assert.Contains("ELASTIC-FALLBACK", rejected.Output);
        // Previously accepted documents with the same SmokeTestId cannot pass this run.
        Assert.Equal(3, fixture.Records.Length);
        Assert.Empty(fixture.ContractErrors);
    }

    [Fact]
    [Trait("Category", "LocalHttpFixture")]
    public async Task Http403FailsDeliveryAndFallsBackToConsole()
    {
        await using var fixture = await ElasticHttpFixture.StartAsync();
        fixture.BulkStatus = 403;
        var result = await ProbeRunner.RunAsync(typeof(SmokeTests).Assembly, Settings(fixture));
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("not delivered", result.Error);
        Assert.Contains("ELASTIC-FALLBACK", result.Output);
        Assert.Empty(fixture.Records);
    }

    [Fact]
    [Trait("Category", "LocalHttpFixture")]
    public async Task CollidingRoutesRetainAllThreeEvents()
    {
        await using var fixture = await ElasticHttpFixture.StartAsync();
        var result = await ProbeRunner.RunAsync(typeof(SmokeTests).Assembly, Settings(fixture, "ledms-rabbitmq"));
        Assert.True(result.ExitCode == 0, result.Output + result.Error);
        Assert.Equal(3, fixture.Records.Length);
        Assert.Equal(2, fixture.Records.Select(record => record.Index).Distinct().Count());
        Assert.Empty(fixture.ContractErrors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [Trait("Category", "LocalHttpFixture")]
    public async Task DefaultSilentBootstrapContinuesAfterTemplate403(string bootstrap)
    {
        await using var fixture = await ElasticHttpFixture.StartAsync();
        fixture.DenyTemplates = true;
        var settings = Settings(fixture);
        settings["ELASTIC_BOOTSTRAP_METHOD"] = bootstrap;
        var result = await ProbeRunner.RunAsync(typeof(SmokeTests).Assembly, settings);
        Assert.True(result.ExitCode == 0, result.Output + result.Error);
        Assert.Equal(3, fixture.Records.Length);
        Assert.Contains(fixture.Requests, request => request.Method == "PUT" && (request.Path.Contains("/_template/") || request.Path.Contains("/_index_template/")));
        Assert.Empty(fixture.ContractErrors);
    }

    [Fact]
    [Trait("Category", "LocalHttpFixture")]
    public async Task VersionMismatchFailsBeforeSendingLogs()
    {
        await using var fixture = await ElasticHttpFixture.StartAsync();
        var settings = Settings(fixture);
        settings["COMPAT_EXPECTED_ELASTIC"] = "7.16.1";
        var result = await ProbeRunner.RunAsync(typeof(SmokeTests).Assembly, settings);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Expected Elasticsearch 7.16.1; server reports 8.15.0.", result.Error);
        Assert.Empty(fixture.Records);
    }
}
