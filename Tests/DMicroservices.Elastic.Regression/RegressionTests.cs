using DMicroservices.Elastic.TestSupport;
using Xunit;

namespace DMicroservices.Elastic.Regression;

public class RegressionTests
{
    [Fact]
    [Trait("Category", "LocalRegression")]
    public async Task CredentialsRoutesFileLoggingAndConcurrentShutdown()
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
