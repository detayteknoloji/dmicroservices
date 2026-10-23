using System.Diagnostics;
using System.Reflection;

namespace DMicroservices.Elastic.TestSupport;

internal sealed record ProbeResult(int ExitCode, string Output, string Error);

internal static class ProbeRunner
{
    public static async Task<ProbeResult> RunAsync(Assembly assembly,
        IDictionary<string, string> environment = null, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(assembly.Location);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        // A developer's real endpoints/credentials must never enter local fixture tests.
        foreach (var key in new[] { "ELASTIC_URI", "ELASTIC_USERNAME", "ELASTIC_PASSWORD", "ELASTIC_URI_SSL",
            "ELASTIC_VERIFY_USERNAME", "ELASTIC_VERIFY_PASSWORD", "ELASTIC_BOOTSTRAP_METHOD", "FILE_LOG_LOCATION",
            "LOG_INDEX_FORMAT", "IS_FILE_LOG", "SMOKE_TEST_ID", "SMOKE_VERIFY_TIMEOUT_SECONDS", "COMPAT_EXPECTED_ELASTIC", "COMPAT_VERIFY_HELPER" })
            start.Environment.Remove(key);
        if (environment != null)
            foreach (var pair in environment)
                if (pair.Value == null) start.Environment.Remove(pair.Key);
                else start.Environment[pair.Key] = pair.Value;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the .NET probe.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException("Local probe timed out.\n" + await output + "\n" + await error);
        }
        var capturedOutput = await output;
        var capturedError = await error;
        if (!capturedOutput.Contains("RUNTIME: 5.0."))
            throw new InvalidOperationException("Probe did not prove .NET 5 execution.\n" + capturedOutput + capturedError);
        return new ProbeResult(process.ExitCode, capturedOutput, capturedError);
    }
}
