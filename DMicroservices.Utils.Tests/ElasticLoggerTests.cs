using DMicroservices.Utils.Logger;

namespace DMicroservices.Utils.Tests;

public class ElasticLoggerTests
{
    [Theory]
    [InlineData("info-Serilog-{0:yyyy.MM}", "info-serilog-2026.10")]
    [InlineData("error-ledms-orders-{0:yyyy.MM.dd}", "error-ledms-orders-2026.10.07")]
    [InlineData("info-ledms-rabbitmq", "info-ledms-rabbitmq")]
    public void RoutesKeepDatedIndexNames(string format, string expected)
        => Assert.Equal(expected, ElasticLogger.FormatIndexName(format, new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero)));

    [Theory]
    [InlineData("")]
    [InlineData("info/orders-{0:yyyy.MM}")]
    [InlineData("info-{service}")]
    public void InvalidRoutesAreRejected(string format)
        => Assert.Throws<ArgumentException>(() => ElasticLogger.FormatIndexName(format, DateTimeOffset.UtcNow));

    [Fact]
    public void SearchTargetReplacesDatePlaceholder()
        => Assert.Equal("error-serilog-*", ElasticLogger.GetLogSearchTarget("error-serilog-{0:yyyy.MM}"));

    [Theory]
    [InlineData("http://user:secret@localhost:9200", null, null)]
    [InlineData("https://user:secret@localhost:9200", "env", null)]
    [InlineData("https://user@localhost:9200", null, null)]
    public void ConnectionSettingsDoNotBreakDataAccess(string address, string username, string password)
    {
        var connection = ElasticConnectionSettings.Resolve(address, username, password);
        Assert.Equal("user", connection.Username);
        Assert.Equal(string.Empty, connection.Endpoint.UserInfo);
    }
}
