using System.Diagnostics;
using DMicroservices.Utils.Logger;

namespace DMicroservices.Elastic.Regression;

// Elasticsearch erişilemezken business thread'lerinin ne kadar etkilendiğini ölçen probe modları.
internal static class OutageProbes
{
    public static Task<bool> TryRunAsync(string[] args)
    {
        string mode = args.FirstOrDefault(argument => argument.StartsWith("--outage-"));
        if (mode == null) return Task.FromResult(false);
        Environment.SetEnvironmentVariable("IS_FILE_LOG", "false");
        Environment.SetEnvironmentVariable("LOG_INDEX_FORMAT", "outage-{0:yyyy.MM}");
        switch (mode)
        {
            case "--outage-first-log": FirstLog(); break;
            default: throw new ArgumentException("Unknown outage probe: " + mode);
        }
        return Task.FromResult(true);
    }

    private static void Measure(string name, Stopwatch watch) => Console.WriteLine("MEASURE " + name + "=" + watch.ElapsedMilliseconds);

    // İlk erişim, yeni index'e ilk log ve normal log akışı süreleri.
    private static void FirstLog()
    {
        var watch = Stopwatch.StartNew();
        var logger = ElasticLogger.Instance;
        Measure("ctor_ms", watch);
        Console.WriteLine("CONFIGURED=" + logger.IsConfigured);
        watch.Restart();
        logger.InfoSpecificIndexFormat("OUTAGE_FIRST_SPECIFIC", "outage-specific");
        Measure("first_specific_ms", watch);
        watch.Restart();
        logger.InfoSpecificIndexFormat("OUTAGE_SECOND_SPECIFIC", "outage-specific");
        Measure("second_specific_ms", watch);
        watch.Restart();
        for (int i = 0; i < 1000; i++) logger.Info("OUTAGE_EVENT");
        logger.Error(new InvalidOperationException("OUTAGE_EXCEPTION"), "OUTAGE_ERROR");
        Measure("info_1000_ms", watch);
        Console.WriteLine("PASS: outage first log");
    }
}
