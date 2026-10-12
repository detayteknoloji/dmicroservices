using DMicroservices.Elastic.TestSupport;
using Xunit;

namespace DMicroservices.Elastic.Tls.Regression;

public class CertificateTrustTests
{
    [Fact]
    [Trait("Category", "LocalRegression")]
    public async Task PrivateCaValidationPreservesPlatformTrustAndRejectsInvalidCertificates()
    {
        var result = await ProbeRunner.RunAsync(typeof(CertificateTrustTests).Assembly);
        Assert.True(result.ExitCode == 0, result.Output + result.Error);
        Assert.Contains("PASS: private CA", result.Output);
    }
}
