using Serilog;
using Serilog.Sinks.Elasticsearch;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace DMicroservices.Utils.Logger
{
    public class ElasticLogger
    {
        private static Serilog.Core.Logger _errorLogger;
        private static Serilog.Core.Logger _infoLogger;
        private static bool IsFileLog = Environment.GetEnvironmentVariable("IS_FILE_LOG")?.ToLower() == "true";
        private static readonly bool IsConsoleLogForFileMode = Environment.GetEnvironmentVariable("IS_CONSOLE_LOG")?.ToLower() == "true";
        private static string FileLogLocation = Environment.GetEnvironmentVariable("FILE_LOG_LOCATION");

        public bool IsConfigured { get; set; } = false;

        #region Singleton Section
        private static readonly Lazy<ElasticLogger> _instance = new Lazy<ElasticLogger>(() => new ElasticLogger());

        private ElasticLogger()
        {
            if (IsFileLog)
            {

                if (string.IsNullOrWhiteSpace(FileLogLocation) || !Directory.Exists(FileLogLocation))
                {
                    FileLogLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                }
                ConfigureFileLog();
            }
            else
            {
                bool environmentNotCorrect = false;

                string elasticUri = Environment.GetEnvironmentVariable("ELASTIC_URI");
                string format = Environment.GetEnvironmentVariable("LOG_INDEX_FORMAT");

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
                _errorLogger.Error(ex, messageTemplate);

#if DEBUG
            Debug.WriteLine($"***********************************\nThrow an exception : {ex.Message}\n{messageTemplate}\n{ex.StackTrace}***********************************\n");
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
                _errorLogger.Error(ex, messageTemplate, companyNo);

#if DEBUG
            Debug.WriteLine($"***********************************\nThrow an exception : {ex.Message}\n{messageTemplate}\n{ex.StackTrace}***********************************\n");
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
                _errorLogger.Error(ex, messageTemplate, Convert.ToString(trackObject));

#if DEBUG
            Debug.WriteLine($"***********************************\nThrow an exception : {ex.Message}\n{messageTemplate}\n{ex.StackTrace}***********************************\n");
#endif
        }

        public void Error(Exception ex, string messageTemplate, Dictionary<string, object> parameters)
        {
            if (messageTemplate == null)
                return;

            StringBuilder stringBuilder = new StringBuilder(messageTemplate);

            stringBuilder.AppendLine($", Parent: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}");

            if (IsConfigured)
            {
                foreach (var parameter in parameters)
                {
                    stringBuilder.Append("{");
                    stringBuilder.Append(parameter.Key);
                    stringBuilder.AppendLine("}");
                }

                _errorLogger.Error(ex, stringBuilder.ToString(), parameters.ToList().Select(x => x.Value).ToArray());
            }

#if DEBUG
            Debug.WriteLine($"***********************************\nThrow an exception : {ex.Message}\n{messageTemplate}\n{ex.StackTrace}***********************************\n");
#endif
        }

        public void Info(string messageTemplate)
        {
            if (messageTemplate == null)
                messageTemplate = $"Parent: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}";
            else
                messageTemplate += $", Parent: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}";

            if (IsConfigured)
                _infoLogger.Information(messageTemplate);

#if DEBUG
            Debug.WriteLine($"***********************************\nInformation : {messageTemplate}***********************************\n");
#endif
        }

        public void Info(string messageTemplate, Dictionary<string, object> parameters)
        {
            if (messageTemplate == null)
                return;

            StringBuilder stringBuilder = new StringBuilder(messageTemplate);

            stringBuilder.AppendLine($", Parent: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}");

            if (IsConfigured)
            {
                foreach (var parameter in parameters)
                {
                    stringBuilder.Append("{");
                    stringBuilder.Append(parameter.Key);
                    stringBuilder.AppendLine("}");
                }

                _infoLogger.Information(stringBuilder.ToString(), parameters.ToList().Select(x => x.Value).ToArray());
            }

#if DEBUG
            Debug.WriteLine($"***********************************\nInformation : {messageTemplate}***********************************\n");
#endif
        }

        private void Configure(string elasticUri, string format)
        {
            ConfigureElasticLogger(elasticUri, $"error-{format}", ref _errorLogger);
            ConfigureElasticLogger(elasticUri, $"info-{format}", ref _infoLogger);

            IsConfigured = true;
        }

        private void ConfigureFileLog()
        {
            string outputTemplate = "-----------------------------------{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}-----------------------------------{NewLine}{Exception}-----------------------------------LOG END LINE-----------------------------------{NewLine}{NewLine}";
            ConfigureFileLogger("error", outputTemplate, ref _errorLogger);
            outputTemplate = "-----------------------------------{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}-----------------------------------{NewLine}{NewLine}";
            ConfigureFileLogger("info", outputTemplate, ref _infoLogger);

            IsConfigured = true;
        }

        private void ConfigureFileLogger(string indexName, string outputTemplate, ref Serilog.Core.Logger logger)
        {
            var loggerConfiguration = new LoggerConfiguration()
              .MinimumLevel.Verbose()
              .WriteTo.File(Path.Combine(FileLogLocation, "logs", indexName + "-.txt"), fileSizeLimitBytes: 40971520, rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true,
              outputTemplate: outputTemplate);

            if (IsConsoleLogForFileMode)
                loggerConfiguration.WriteTo.Console(outputTemplate: outputTemplate);

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POD_NAME")))
                loggerConfiguration.Enrich.WithProperty("PodName", Environment.GetEnvironmentVariable("POD_NAME"));

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HOSTNAME")))
                loggerConfiguration.Enrich.WithProperty("PodId", Environment.GetEnvironmentVariable("HOSTNAME"));

            logger = loggerConfiguration.CreateLogger();
        }

        private void ConfigureElasticLogger(string elasticUri, string indexFormat, ref Serilog.Core.Logger logger)
        {
            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Elasticsearch(
                    new ElasticsearchSinkOptions(new Uri(elasticUri))
                    {
                        AutoRegisterTemplate = true,
                        AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv6,
                        TemplateName = "serilog-events-template",
                        IndexFormat = indexFormat
                    });

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POD_NAME")))
                loggerConfiguration.Enrich.WithProperty("PodName", Environment.GetEnvironmentVariable("POD_NAME"));

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HOSTNAME")))
                loggerConfiguration.Enrich.WithProperty("PodId", Environment.GetEnvironmentVariable("HOSTNAME"));

            logger = loggerConfiguration.CreateLogger();
        }
    }
}