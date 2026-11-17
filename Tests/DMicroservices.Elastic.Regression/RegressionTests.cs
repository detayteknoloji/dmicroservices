using DMicroservices.Elastic.TestSupport;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace DMicroservices.Elastic.Regression;

public class RegressionTests
{
    private readonly ITestOutputHelper output;
    public RegressionTests(ITestOutputHelper output) { this.output = output; }

    private static long Measured(ProbeResult result, string name) =>
        long.Parse(Regex.Match(result.Output, "MEASURE " + name + @"=(-?\d+)").Groups[1].Value);

    private static int Lines(string text, string marker) =>
        text.Split('\n').Count(line => line.Contains(marker));

    private static string ClosedEndpoint()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return "http://127.0.0.1:" + port;
    }

    private void Report(ProbeResult result, System.Diagnostics.Stopwatch total)
    {
        foreach (Match match in Regex.Matches(result.Output, @"MEASURE \S+")) output.WriteLine(match.Value);
        output.WriteLine("process_total_ms=" + total.ElapsedMilliseconds);
        output.WriteLine("stdout_fallback_lines=" + Lines(result.Output, "ELASTIC-FALLBACK") + " stdout_bytes=" + result.Output.Length);
        output.WriteLine("stderr_error_lines=" + Lines(result.Error, "[ELASTIC-ERROR]") + " stderr_bytes=" + result.Error.Length);
    }

    [Fact]
    [Trait("Category", "LocalRegression")]
    public async Task UnreachableElasticDoesNotBlockBusinessLogging()
    {
        var total = System.Diagnostics.Stopwatch.StartNew();
        var result = await ProbeRunner.RunAsync(typeof(RegressionTests).Assembly, new Dictionary<string, string>
        {
            ["ELASTIC_URI"] = ClosedEndpoint()
        }, "--outage-first-log");
        Report(result, total);
        Assert.True(result.ExitCode == 0, result.Output + result.Error);
        Assert.Contains("PASS: outage first log", result.Output);
        // Logger kurulduktan sonra log çağrıları ağa dokunmaz.
        Assert.InRange(Measured(result, "second_specific_ms"), 0, 200);
        Assert.InRange(Measured(result, "info_1000_ms"), 0, 3000);
    }

    [Fact]
    [Trait("Category", "LocalHttpFixture")]
    public async Task HangingElasticDoesNotBlockLoggingAfterSetup()
    {
        await using var fixture = await ElasticHttpFixture.StartAsync();
        fixture.Delay = TimeSpan.FromSeconds(20);
        var total = System.Diagnostics.Stopwatch.StartNew();
        var result = await ProbeRunner.RunAsync(typeof(RegressionTests).Assembly, new Dictionary<string, string>
        {
            ["ELASTIC_URI"] = fixture.Address
        }, "--outage-first-log");
        Report(result, total);
        Assert.True(result.ExitCode == 0, result.Output + result.Error);
        Assert.InRange(Measured(result, "second_specific_ms"), 0, 200);
        Assert.InRange(Measured(result, "info_1000_ms"), 0, 3000);
    }

    [Fact]
    [Trait("Category", "LocalRegression")]
    public async Task CredentialsRoutesFileLoggingAndConcurrentLogging()
    {
        var result = await ProbeRunner.RunAsync(typeof(RegressionTests).Assembly);
        Assert.True(result.ExitCode == 0, result.Output + result.Error);
        Assert.Contains("PASS: URI/env auth", result.Output);
    }

    [Fact]
    [Trait("Category", "LocalHttpFixture")]
    public async Task FailedTemplateBootstrapKeepsBusinessLoggingUsable()
    {
        await using var fixture = await ElasticHttpFixture.StartAsync();
        fixture.DenyTemplates = true;
        var result = await ProbeRunner.RunAsync(typeof(RegressionTests).Assembly,
            new Dictionary<string, string> { ["ELASTIC_URI"] = fixture.Address }, "--setup-failure");
        Assert.True(result.ExitCode == 0, result.Output + result.Error);
        Assert.Contains("PASS: failed bootstrap", result.Output);
        Assert.Contains(fixture.Requests, request => request.Method == "PUT" && (request.Path.Contains("/_template/") || request.Path.Contains("/_index_template/")));
        Assert.Contains("BUSINESS_CONTINUES", result.Output);
    }
}
