using Serilog;
using Serilog.Sinks.Elasticsearch;
using System;
using System.Diagnostics;

namespace DMicroservices.Utils.Logger
{
    public class ElasticLogger
    {
        private static Serilog.Core.Logger _log;
        public bool IsConfigured { get; set; } = false;

        #region Singleton Section
        private static readonly Lazy<ElasticLogger> _instance = new Lazy<ElasticLogger>(() => new ElasticLogger());

        private ElasticLogger()
        {
            string elasticUri = Environment.GetEnvironmentVariable("ELASTIC_URI");
            string format = Environment.GetEnvironmentVariable("LOG_INDEX_FORMAT");

            bool environmentNotCorrect = false;
            if (string.IsNullOrEmpty(elasticUri))
            {
                Console.WriteLine("env:ELASTIC_URI is empty.");
                environmentNotCorrect = true;
            }

            if (string.IsNullOrEmpty(format))
            {
                Console.WriteLine("env:LOG_INDEX_FORMAT is empty.");
                environmentNotCorrect = true;
            }

            if (!environmentNotCorrect)
                Configure(elasticUri, format);
        }

        public static ElasticLogger Instance => _instance.Value;
        #endregion

        public void Error(Exception ex, string messageTemplate)
        {
            if (messageTemplate == null)
                messageTemplate = $"Parent: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}";
            else
                messageTemplate += $", Parent: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}";

            if (IsConfigured)
                _log.Error(ex, messageTemplate);

#if DEBUG
            Debug.WriteLine($"***********************************\nThrow an exception : {messageTemplate}\n{ex.StackTrace}***********************************\n");
#endif
        }

        public void Error(Exception ex, string messageTemplate, string companyNo)
        {
            if (messageTemplate == null)
                messageTemplate = $"Parent: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}";
            else
                messageTemplate += $", Parent: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}";

            messageTemplate += " by Company Id :{@CompanyNo}";

            if (IsConfigured)
                _log.Error(ex, messageTemplate, companyNo);

#if DEBUG
            Debug.WriteLine($"***********************************\nThrow an exception : {messageTemplate}\n{ex.StackTrace}***********************************\n");
#endif
        }


        public void Error(Exception ex, string messageTemplate, object trackObject)
        {
            if (messageTemplate == null)
                messageTemplate = $"Parent: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}";
            else
                messageTemplate += $", Parent: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}";

            messageTemplate += " with Track object : {@trackObject}";

            if (IsConfigured)
                _log.Error(ex, messageTemplate, Convert.ToString(trackObject));

#if DEBUG
            Debug.WriteLine($"***********************************\nThrow an exception : {messageTemplate}\n{ex.StackTrace}***********************************\n");
#endif
        }

        public void Info(string messageTemplate)
        {
            if (messageTemplate == null)
                messageTemplate = $"Parent: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}";
            else
                messageTemplate += $", Parent: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}";

            if (IsConfigured)
                _log.Information(messageTemplate);

#if DEBUG
            Debug.WriteLine($"***********************************\nInformation : {messageTemplate}***********************************\n");
#endif
        }

        private void Configure(string elasticUri, string format)
        {// ex.  "serilog-{0:yyyy.MM.dd}"

            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Elasticsearch(
                    new ElasticsearchSinkOptions(new Uri(elasticUri))
                    {
                        AutoRegisterTemplate = true,
                        AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv6,
                        TemplateName = "serilog-events-template",
                        IndexFormat = format
                    });

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POD_NAME")))
                loggerConfiguration.Enrich.WithProperty("PodName", Environment.GetEnvironmentVariable("POD_NAME"));

            _log = loggerConfiguration.CreateLogger();

            IsConfigured = true;
        }
    }
}