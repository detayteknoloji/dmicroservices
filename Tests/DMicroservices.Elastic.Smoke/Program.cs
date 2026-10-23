using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DMicroservices.Utils.Logger;
using DMicroservices.Elastic.Smoke;

Console.WriteLine("RUNTIME: " + Environment.Version);
if (Environment.Version.Major != 5) throw new InvalidOperationException("This probe requires .NET 5.");
if (args.Length > 0 && args[0] == "--prepare-fixture")
{
    if (args.Length != 2) throw new ArgumentException("Expected a TLS fixture directory.");
    CompatibilityChecks.PrepareFixture(args[1]);
    return;
}
await CompatibilityChecks.WaitForExpectedVersionAsync();

// Exit successfully only after all three routes are searchable in Elasticsearch.
Serilog.Debugging.SelfLog.Enable(System.IO.TextWriter.Synchronized(Console.Error));
var logger = ElasticLogger.Instance;
if (!logger.IsConfigured)
    throw new InvalidOperationException("Elasticsearch logger is not configured.");
var id = Environment.GetEnvironmentVariable("SMOKE_TEST_ID") ?? Guid.NewGuid().ToString("N");
var runId = Guid.NewGuid().ToString("N");
try
{
    var properties = new Dictionary<string, object> { ["SmokeTestId"] = id, ["SmokeRunId"] = runId };
    logger.Info("Elasticsearch smoke info ", properties);
    logger.Error(new InvalidOperationException("Expected smoke test exception"), "Elasticsearch smoke error ", properties);
    logger.InfoSpecificIndexFormat("Elasticsearch smoke specific ", "ledms-rabbitmq", properties);
    Console.WriteLine($"SmokeTestId={id}; SmokeRunId={runId}; waiting for searchable info, error and specific events.");
}
finally
{
    logger.CloseAndFlush();
}

var address = Environment.GetEnvironmentVariable("ELASTIC_URI");
var verifyUser = Environment.GetEnvironmentVariable("ELASTIC_VERIFY_USERNAME");
var verifyPassword = Environment.GetEnvironmentVariable("ELASTIC_VERIFY_PASSWORD");
var connection = string.IsNullOrWhiteSpace(verifyUser) && string.IsNullOrEmpty(verifyPassword)
    ? ElasticConnectionSettings.FromEnvironment(address)
    : ElasticConnectionSettings.Resolve(address, verifyUser, verifyPassword);
var handler = new HttpClientHandler();
var certificateValidation = ElasticCertificateTrust.CreateValidationCallback(connection.Endpoint);
if (certificateValidation != null)
    handler.ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) => certificateValidation(request, certificate, chain, errors);
using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
if (!string.IsNullOrEmpty(connection.Username))
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
        Convert.ToBase64String(Encoding.UTF8.GetBytes(connection.Username + ":" + connection.Password)));
var route = Environment.GetEnvironmentVariable("LOG_INDEX_FORMAT");
var streams = new[]
{
    ElasticLogger.GetLogSearchTarget("info-" + route),
    ElasticLogger.GetLogSearchTarget("error-" + route),
    ElasticLogger.GetLogSearchTarget("info-ledms-rabbitmq")
}.GroupBy(stream => stream).ToDictionary(group => group.Key, group => group.Count());
int seconds = int.TryParse(Environment.GetEnvironmentVariable("SMOKE_VERIFY_TIMEOUT_SECONDS"), out var configuredSeconds)
    ? Math.Max(1, Math.Min(120, configuredSeconds)) : 60;
var deadline = DateTime.UtcNow.AddSeconds(seconds);
const string propertyPrefix = "fields.";
object IdentifierQuery(string field, string value) => new { @bool = new { should = new object[]
{
    new { term = new Dictionary<string, string> { [propertyPrefix + field] = value } },
    new { term = new Dictionary<string, string> { [propertyPrefix + field + ".keyword"] = value } }
}, minimum_should_match = 1 } };
var query = JsonSerializer.Serialize(new
{
    size = 0,
    query = new { @bool = new { must = new[] { IdentifierQuery("SmokeTestId", id), IdentifierQuery("SmokeRunId", runId) } } }
});
while (streams.Count > 0 && DateTime.UtcNow < deadline)
{
    foreach (var expected in streams.ToArray())
    {
        var stream = expected.Key;
        using var response = await http.PostAsync(connection.Endpoint.AbsoluteUri.TrimEnd('/') + "/" + stream + "/_search",
            new StringContent(query, Encoding.UTF8, "application/json"));
        if ((int)response.StatusCode == 404) continue;
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Smoke verification HTTP " + (int)response.StatusCode + "; requires read permission on each stream.");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (document.RootElement.GetProperty("hits").GetProperty("total").GetProperty("value").GetInt64() >= expected.Value)
            streams.Remove(stream);
    }
    if (streams.Count > 0) await Task.Delay(1000);
}
if (streams.Count > 0) throw new TimeoutException("Smoke events were not delivered to: " + string.Join(", ", streams.Keys));
Console.WriteLine($"PASS: SmokeTestId={id}; SmokeRunId={runId}; all three events are searchable in Elasticsearch.");

await CompatibilityChecks.VerifyHelperAsync();
