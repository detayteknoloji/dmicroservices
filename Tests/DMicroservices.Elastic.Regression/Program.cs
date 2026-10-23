using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using DMicroservices.Utils.Logger;

Console.WriteLine("RUNTIME: " + Environment.Version);
if (Environment.Version.Major != 5) throw new InvalidOperationException("This probe requires .NET 5.");

if (args.Contains("--setup-failure"))
{
    Environment.SetEnvironmentVariable("IS_FILE_LOG", "false");
    Environment.SetEnvironmentVariable("ELASTIC_BOOTSTRAP_METHOD", "Failure");
    Environment.SetEnvironmentVariable("LOG_INDEX_FORMAT", "setup-regression");
    // The endpoint is supplied by the local fixture; production credentials are never used.
    Environment.SetEnvironmentVariable("ELASTIC_USERNAME", null);
    Environment.SetEnvironmentVariable("ELASTIC_PASSWORD", null);
    var output = new StringWriter();
    var original = Console.Error;
    Console.SetError(output);
    try
    {
        var failed = ElasticLogger.Instance;
        if (failed.IsConfigured) throw new Exception("Failed bootstrap was marked configured.");
        if (!ReferenceEquals(failed, ElasticLogger.Instance)) throw new Exception("Lazy singleton was poisoned.");
        failed.Info("BUSINESS_CONTINUES");
        failed.Error(new Exception("Business error"), "BUSINESS_CONTINUES");
        failed.InfoSpecificIndexFormat("BUSINESS_CONTINUES", "failed-route");
        failed.InfoSpecificIndexFormat("BUSINESS_CONTINUES", "failed-route", new Dictionary<string, object> { ["Id"] = 1 });
        failed.ErrorSpecificIndexFormat(new Exception("Business error"), "BUSINESS_CONTINUES", "failed-route");
        failed.CloseAndFlush();
        failed.CloseAndFlush();
        if (!output.ToString().Contains("Elasticsearch sink setup failed")) throw new Exception("Failure was not printed to console.");
    }
    finally { Console.SetError(original); }
    Console.WriteLine("PASS: failed bootstrap is caught for main/specific loggers, Lazy remains usable and console reports the failure.");
    return;
}

var directory = Path.Combine(Path.GetTempPath(), "elastic-regression-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
Environment.SetEnvironmentVariable("IS_FILE_LOG", "true");
Environment.SetEnvironmentVariable("FILE_LOG_LOCATION", directory);
void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
void Reject(Action action)
{
    try { action(); } catch (ArgumentException) { return; }
    throw new Exception("Invalid credentials/route were accepted.");
}
foreach (var pair in new[] { ("", ""), ((string)null, (string)null) })
{
    var connection = ElasticConnectionSettings.Resolve("https://u%40x:p%3Aa%2F%40%25@localhost:9200", pair.Item1, pair.Item2);
    Check(connection.Username == "u@x" && connection.Password == "p:a/@%", "URI decode/fallback failed.");
    Check(connection.Endpoint.UserInfo == "" && !connection.ToString().Contains("p:a"), "Credentials exposed in endpoint.");
}
var overridden = ElasticConnectionSettings.Resolve("https://uri:secret@localhost:9200", "env", "env-secret");
Check(overridden.Username == "env" && overridden.Password == "env-secret", "Env pair did not override URI pair.");
// Data-access callers must keep working: partial env pairs and HTTP Basic Auth only warn.
Check(ElasticConnectionSettings.Resolve("https://uri:secret@localhost:9200", "env", null).Username == "uri", "Partial env pair broke URI fallback.");
Check(ElasticConnectionSettings.Resolve("https://uri:secret@localhost:9200", "", "secret").Username == "uri", "Partial env pair broke URI fallback.");
Check(ElasticConnectionSettings.Resolve("http://user:secret@localhost:9200", null, null).Password == "secret", "HTTP Basic Auth was rejected.");
Reject(() => ElasticConnectionSettings.Resolve("file:///tmp/log", null, null));
Check(ElasticLogger.FormatIndexName("info-Serilog-{0:yyyy.MM}", new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero)) == "info-serilog-2026.10", "Monthly index route changed.");
Check(ElasticLogger.GetLogSearchTarget("error-serilog-{0:yyyy.MM}") == "error-serilog-*", "Index search target changed.");
Reject(() => ElasticLogger.FormatIndexName("bad/{0:yyyy.MM}", DateTimeOffset.UtcNow));
Reject(() => ElasticLogger.FormatIndexName("bad-{other}", DateTimeOffset.UtcNow));
var errorOutput = new StringWriter();
var previousError = Console.Error;
Environment.SetEnvironmentVariable("ELASTIC_USERNAME", "regression-user");
Environment.SetEnvironmentVariable("ELASTIC_PASSWORD", "regression-secret:@");
Console.SetError(errorOutput);
try { ElasticLogger.ReportLoggerFailure("test", new Exception("regression-user regression-secret:@ regression-secret%3A%40")); }
finally { Console.SetError(previousError); }
Check(errorOutput.ToString().Contains("[REDACTED]") && !errorOutput.ToString().Contains("regression-user")
    && !errorOutput.ToString().Contains("regression-secret"), "Console diagnostic leaked credentials.");
Environment.SetEnvironmentVariable("ELASTIC_USERNAME", null);
Environment.SetEnvironmentVariable("ELASTIC_PASSWORD", null);
var logger = ElasticLogger.Instance;
logger.Info("BEFORE_CLOSE_INFO");
logger.Error(new InvalidOperationException("BEFORE_CLOSE_EXCEPTION"), "BEFORE_CLOSE_ERROR");
logger.InfoSpecificIndexFormatInFile("BEFORE_CLOSE_SPECIFIC", "regression", directory);
await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
{
    for (int i = 0; i < 100; i++) logger.InfoSpecificIndexFormatInFile("CONCURRENT", "worker" + worker, directory);
})).Concat(new[] { Task.Run(() => logger.CloseAndFlush()) }));
logger.CloseAndFlush();
logger.Info("AFTER_CLOSE");
logger.InfoSpecificIndexFormatInFile("AFTER_CLOSE", "after-close", directory);
logger.InfoSpecificIndexFormat("AFTER_CLOSE", "after-close");
var content = string.Join("\n", Directory.GetFiles(directory, "*.txt", SearchOption.AllDirectories).Select(File.ReadAllText));
Check(!logger.IsConfigured && content.Contains("BEFORE_CLOSE_INFO") && content.Contains("BEFORE_CLOSE_ERROR")
    && content.Contains("BEFORE_CLOSE_EXCEPTION") && content.Contains("BEFORE_CLOSE_SPECIFIC") && !content.Contains("AFTER_CLOSE"), "File/lifecycle regression.");
Console.WriteLine("PASS: URI/env auth, route validation, file logging, concurrent terminal shutdown on " + Environment.Version);
