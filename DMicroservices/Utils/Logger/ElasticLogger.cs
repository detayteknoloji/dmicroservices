using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Globalization;

namespace DMicroservices.Utils.Logger
{
    public class ElasticLogger
    {
        private static Serilog.Core.Logger _errorLogger;
        private static Serilog.Core.Logger _infoLogger;
        private static Serilog.Core.Logger _consoleErrorLogger;
        private static Serilog.Core.Logger _consoleInfoLogger;
        private static Serilog.Core.Logger ErrorLogger => _errorLogger ?? _consoleErrorLogger;
        private static Serilog.Core.Logger InfoLogger => _infoLogger ?? _consoleInfoLogger;
        private static string _fileLogLocation = Environment.GetEnvironmentVariable("FILE_LOG_LOCATION");
        private static readonly bool IsFileLog = Environment.GetEnvironmentVariable("IS_FILE_LOG")?.ToLower() == "true";
        private static readonly string ElasticUri = Environment.GetEnvironmentVariable("ELASTIC_URI");

        private static readonly ConcurrentDictionary<string, Tuple<bool, Serilog.Core.Logger>> SpecificIndexFormat = new ConcurrentDictionary<string, Tuple<bool, Serilog.Core.Logger>>();
        private static readonly ConcurrentDictionary<string, Tuple<bool, Serilog.Core.Logger>> SpecificIndexFileFormat = new ConcurrentDictionary<string, Tuple<bool, Serilog.Core.Logger>>();

        private readonly ReaderWriterLockSlim _lifecycle = new ReaderWriterLockSlim();
        private volatile bool _closed;
        private volatile bool _configured;
        public bool IsConfigured { get => _configured && !_closed; set => _configured = value && !_closed; }
        public static void CloseAndFlushIfCreated() { if (_instance.IsValueCreated) _instance.Value.CloseAndFlush(); }

        #region Singleton Section
        private static readonly Lazy<ElasticLogger> _instance = new Lazy<ElasticLogger>(() => new ElasticLogger());

        private ElasticLogger()
        {
            AppDomain.CurrentDomain.ProcessExit += (_, __) => CloseAndFlushIfCreated();
            string fallbackFormat = Environment.GetEnvironmentVariable("LOG_INDEX_FORMAT");
            _consoleErrorLogger = CreateConsoleFallbackLogger($"error-{fallbackFormat}");
            _consoleInfoLogger = CreateConsoleFallbackLogger($"info-{fallbackFormat}");
            if (IsFileLog)
            {
                if (string.IsNullOrWhiteSpace(_fileLogLocation) || !Directory.Exists(_fileLogLocation))
                {
                    _fileLogLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                }
                ConfigureFileLog();
            }
            else
            {
                string format = Environment.GetEnvironmentVariable("LOG_INDEX_FORMAT");

                if (!ElasticUriControl(ElasticUri, format))
                    Configure(ElasticUri, format);
            }
        }

        private static bool ElasticUriControl(string elasticUri, string format)
        {
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

            return environmentNotCorrect;
        }
        public static ElasticLogger Instance => _instance.Value;
        #endregion

        public void Error(Exception ex, string messageTemplate)
        {
            _lifecycle.EnterReadLock();
            try
            {
                if (_closed) return;
            if (messageTemplate == null)
                messageTemplate = $"Parent: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}";
            else
                messageTemplate += $", Parent: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name} ";

            SetStacktraceExceptionSource(ex);
            ErrorLogger?.Error(ex, messageTemplate);

#if DEBUG
            Debug.WriteLine($"***********************************\nThrow an exception : {ex.Message}\n{messageTemplate}\n{ex.StackTrace}***********************************\n");
#endif
            }
            finally { _lifecycle.ExitReadLock(); }
        }

        private readonly object _objectInitializeLock = new object();

        private Serilog.Core.Logger GetSpecificLoggerInstance(string specificIndexFormat)
        {
            if (string.IsNullOrWhiteSpace(ElasticUri))
                return SpecificIndexFormat.GetOrAdd(specificIndexFormat, route => Tuple.Create(true, CreateConsoleFallbackLogger(route))).Item2;

            if (SpecificIndexFormat.TryGetValue(specificIndexFormat, out var specificLoggerItems) && specificLoggerItems.Item1)
            {
                return specificLoggerItems.Item2;
            }
            else
            {
                lock (_objectInitializeLock)
                {
                    if (SpecificIndexFormat.TryGetValue(specificIndexFormat,
                            out specificLoggerItems) && specificLoggerItems.Item1)
                    {
                        return specificLoggerItems.Item2;
                    }

                    Serilog.Core.Logger specificLoggerInstance = null;
                    ConfigureElasticLogger(ElasticUri, specificIndexFormat, ref specificLoggerInstance);
                    if (specificLoggerInstance == null)
                        specificLoggerInstance = CreateConsoleFallbackLogger(specificIndexFormat);
                    SpecificIndexFormat.TryAdd(specificIndexFormat, Tuple.Create(true, specificLoggerInstance));
                    return specificLoggerInstance;
                }
            }
        }

        #region Specific File Elastic Index Format Methods

        public void ErrorSpecificIndexFormatInFile(Exception ex, string messageTemplate, string specificIndexFormat, string logFilePath)
        {
            _lifecycle.EnterReadLock();
            try
            {
                if (_closed) return;
            messageTemplate = CreateMessageTemplate(messageTemplate);
            GetSpecificLoggerFileInstance($"error-{specificIndexFormat}", logFilePath, true)?.Error(ex, messageTemplate);

#if DEBUG
            Debug.WriteLine($"***********************************\n{specificIndexFormat}\nThrow an exception : {ex.Message}\n{messageTemplate}\n{ex.StackTrace}***********************************\n");
#endif
            }
            finally { _lifecycle.ExitReadLock(); }
        }

        public void ErrorSpecificIndexFormatInFile(Exception ex, string messageTemplate, string specificIndexFormat, Dictionary<string, object> parameters, string logFilePath)
        {
            _lifecycle.EnterReadLock();
            try
            {
                if (_closed) return;
            messageTemplate = CreateMessageTemplate(messageTemplate);

            string parameterMessageTemplate = CreateWithParameterMessageTemplate(messageTemplate, parameters);

            GetSpecificLoggerFileInstance($"error-{specificIndexFormat}", logFilePath, true)?.Error(ex, parameterMessageTemplate, parameters.ToList().Select(x => x.Value).ToArray());

#if DEBUG
            Debug.WriteLine($"***********************************\n{specificIndexFormat}\nThrow an exception : {ex.Message}\n{messageTemplate}\n{ex.StackTrace}***********************************\n");
#endif
            }
            finally { _lifecycle.ExitReadLock(); }
        }

        public void InfoSpecificIndexFormatInFile(string messageTemplate, string fileName, string logFilePath)
        {
            _lifecycle.EnterReadLock();
            try
            {
                if (_closed) return;
            messageTemplate = CreateMessageTemplate(messageTemplate);

            GetSpecificLoggerFileInstance($"info-{fileName}", logFilePath, false)?.Information(messageTemplate);

#if DEBUG
            Debug.WriteLine($"***********************************\nInformation : {messageTemplate}***********************************\n");
#endif
            }
            finally { _lifecycle.ExitReadLock(); }
        }

        public void InfoSpecificIndexFormatInFile(string messageTemplate, string specificIndexFormat, Dictionary<string, object> parameters, string logFilePath)
        {
            _lifecycle.EnterReadLock();
            try
            {
                if (_closed) return;
            messageTemplate = CreateMessageTemplate(messageTemplate);

            string parameterMessageTemplate = CreateWithParameterMessageTemplate(messageTemplate, parameters);

            GetSpecificLoggerFileInstance($"info-{specificIndexFormat}", logFilePath, false)?.Information(parameterMessageTemplate, parameters.ToList().Select(x => x.Value).ToArray());

#if DEBUG
            Debug.WriteLine($"***********************************\nInformation : {parameterMessageTemplate}***********************************\n");
#endif
            }
            finally { _lifecycle.ExitReadLock(); }
        }

        #endregion

        #region Specific Elastic Index Format Methods

        public void ErrorSpecificIndexFormat(Exception ex, string messageTemplate, string specificIndexFormat)
        {
            _lifecycle.EnterReadLock();
            try
            {
                if (_closed) return;
            messageTemplate = CreateMessageTemplate(messageTemplate);

            SetStacktraceExceptionSource(ex);
            GetSpecificLoggerInstance($"error-{specificIndexFormat}")?.Error(ex, messageTemplate);

#if DEBUG
            Debug.WriteLine($"***********************************\n{specificIndexFormat}\nThrow an exception : {ex.Message}\n{messageTemplate}\n{ex.StackTrace}***********************************\n");
#endif
            }
            finally { _lifecycle.ExitReadLock(); }
        }

        public void ErrorSpecificIndexFormat(Exception ex, string messageTemplate, string specificIndexFormat, Dictionary<string, object> parameters)
        {
            _lifecycle.EnterReadLock();
            try
            {
                if (_closed) return;
            messageTemplate = CreateMessageTemplate(messageTemplate);

            string parameterMessageTemplate = CreateWithParameterMessageTemplate(messageTemplate, parameters);

            SetStacktraceExceptionSource(ex);
            GetSpecificLoggerInstance($"error-{specificIndexFormat}")?.Error(ex, parameterMessageTemplate, parameters.ToList().Select(x => x.Value).ToArray());

#if DEBUG
            Debug.WriteLine($"***********************************\n{specificIndexFormat}\nThrow an exception : {ex.Message}\n{messageTemplate}\n{ex.StackTrace}***********************************\n");
#endif
            }
            finally { _lifecycle.ExitReadLock(); }
        }

        public void InfoSpecificIndexFormat(string messageTemplate, string specificIndexFormat)
        {
            _lifecycle.EnterReadLock();
            try
            {
                if (_closed) return;
            messageTemplate = CreateMessageTemplate(messageTemplate);

            GetSpecificLoggerInstance($"info-{specificIndexFormat}")?.Information(messageTemplate);

#if DEBUG
            Debug.WriteLine($"***********************************\nInformation : {messageTemplate}***********************************\n");
#endif
            }
            finally { _lifecycle.ExitReadLock(); }
        }

        public void InfoSpecificIndexFormat(string messageTemplate, string specificIndexFormat, Dictionary<string, object> parameters)
        {
            _lifecycle.EnterReadLock();
            try
            {
                if (_closed) return;
            messageTemplate = CreateMessageTemplate(messageTemplate);

            string parameterMessageTemplate = CreateWithParameterMessageTemplate(messageTemplate, parameters);

            GetSpecificLoggerInstance($"info-{specificIndexFormat}")?.Information(parameterMessageTemplate, parameters.ToList().Select(x => x.Value).ToArray());

#if DEBUG
            Debug.WriteLine($"***********************************\nInformation : {parameterMessageTemplate}***********************************\n");
#endif
            }
            finally { _lifecycle.ExitReadLock(); }
        }
        #endregion

        #region Normal Elastic Index Methods
        public void Error(Exception ex, string messageTemplate, string companyNo)
        {
            _lifecycle.EnterReadLock();
            try
            {
                if (_closed) return;
            messageTemplate = CreateMessageTemplate(messageTemplate);

            messageTemplate += " by Company Id :{@CompanyNo}";
            SetStacktraceExceptionSource(ex);
            ErrorLogger?.Error(ex, messageTemplate, companyNo);

#if DEBUG
            Debug.WriteLine($"***********************************\nThrow an exception : {ex.Message}\n{messageTemplate}\n{ex.StackTrace}***********************************\n");
#endif
            }
            finally { _lifecycle.ExitReadLock(); }
        }

        public void Error(Exception ex, string messageTemplate, object trackObject)
        {
            _lifecycle.EnterReadLock();
            try
            {
                if (_closed) return;
            messageTemplate = CreateMessageTemplate(messageTemplate);

            messageTemplate += " with Track object : {@trackObject}";
            SetStacktraceExceptionSource(ex);
            ErrorLogger?.Error(ex, messageTemplate, Convert.ToString(trackObject));

#if DEBUG
            Debug.WriteLine($"***********************************\nThrow an exception : {ex.Message}\n{messageTemplate}\n{ex.StackTrace}***********************************\n");
#endif
            }
            finally { _lifecycle.ExitReadLock(); }
        }

        public void Error(Exception ex, string messageTemplate, Dictionary<string, object> parameters)
        {
            _lifecycle.EnterReadLock();
            try
            {
                if (_closed) return;
            if (messageTemplate == null)
                return;

            StringBuilder stringBuilder = new StringBuilder(messageTemplate);

            stringBuilder.AppendLine($", Parent: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}");

            if (ErrorLogger != null)
            {
                foreach (var parameter in parameters)
                {
                    stringBuilder.Append("{");
                    stringBuilder.Append(parameter.Key);
                    stringBuilder.AppendLine("}");
                }

                ErrorLogger.Error(ex, stringBuilder.ToString(), parameters.ToList().Select(x => x.Value).ToArray());
            }

#if DEBUG
            Debug.WriteLine($"***********************************\nThrow an exception : {ex.Message}\n{messageTemplate}\n{ex.StackTrace}***********************************\n");
#endif
            }
            finally { _lifecycle.ExitReadLock(); }
        }

        public void Info(string messageTemplate)
        {
            _lifecycle.EnterReadLock();
            try
            {
                if (_closed) return;
            messageTemplate = CreateMessageTemplate(messageTemplate);

            InfoLogger?.Information(messageTemplate);

#if DEBUG
            Debug.WriteLine($"***********************************\nInformation : {messageTemplate}***********************************\n");
#endif
            }
            finally { _lifecycle.ExitReadLock(); }
        }

        public void Info(string messageTemplate, Dictionary<string, object> parameters)
        {
            _lifecycle.EnterReadLock();
            try
            {
                if (_closed) return;
            if (messageTemplate == null)
                return;

            StringBuilder stringBuilder = new StringBuilder(messageTemplate);

            stringBuilder.AppendLine($", Parent: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}");

            if (InfoLogger != null)
            {
                foreach (var parameter in parameters)
                {
                    stringBuilder.Append("{");
                    stringBuilder.Append(parameter.Key);
                    stringBuilder.AppendLine("}");
                }

                InfoLogger.Information(stringBuilder.ToString(), parameters.ToList().Select(x => x.Value).ToArray());
            }

#if DEBUG
            Debug.WriteLine($"***********************************\nInformation : {messageTemplate}***********************************\n");
#endif
            }
            finally { _lifecycle.ExitReadLock(); }
        }
        #endregion

        private void Configure(string elasticUri, string format)
        {
            ConfigureElasticLogger(elasticUri, $"error-{format}", ref _errorLogger);
            ConfigureElasticLogger(elasticUri, $"info-{format}", ref _infoLogger);

            IsConfigured = _errorLogger != null && _infoLogger != null;
        }

        private void ConfigureFileLog()
        {
            string outputTemplate = "-----------------------------------{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}-----------------------------------{NewLine}{Exception}-----------------------------------LOG END LINE-----------------------------------{NewLine}{NewLine}";
            ConfigureFileLogger("error", outputTemplate, ref _errorLogger);
            outputTemplate = "-----------------------------------{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}-----------------------------------{NewLine}{NewLine}";
            ConfigureFileLogger("info", outputTemplate, ref _infoLogger);

            IsConfigured = _errorLogger != null && _infoLogger != null;
        }

        private void ConfigureFileLogger(string indexName, string outputTemplate, ref Serilog.Core.Logger logger, string additionalFilePath = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(additionalFilePath))
                {
                    additionalFilePath = _fileLogLocation;
                }

                var combinedPath = Path.Combine(additionalFilePath, "logs", indexName);

                string directory = Path.GetDirectoryName(Path.GetFullPath(combinedPath));
                Directory.CreateDirectory(directory);
                using (new FileStream(Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".probe"), FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 1, FileOptions.DeleteOnClose)) { }
                var loggerConfiguration = new LoggerConfiguration()
                  .MinimumLevel.Verbose()
                  .WriteTo.File($"{combinedPath}-.txt", fileSizeLimitBytes: 40971520, rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true,
                  outputTemplate: outputTemplate, shared: true);

                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POD_NAME")))
                    loggerConfiguration.Enrich.WithProperty("PodName", Environment.GetEnvironmentVariable("POD_NAME"));

                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HOSTNAME")))
                    loggerConfiguration.Enrich.WithProperty("PodId", Environment.GetEnvironmentVariable("HOSTNAME"));

                logger = loggerConfiguration.CreateLogger();
            }
            catch (Exception error)
            {
                logger = null;
                ReportLoggerFailure("file sink setup", error);
            }
        }

        private void ConfigureElasticLogger(string elasticUri, string indexFormat, ref Serilog.Core.Logger logger)
        {
            try
            {
                var connection = ElasticConnectionSettings.FromEnvironment(elasticUri);
                var endpoint = connection.Endpoint;
                var username = connection.Username;
                var password = connection.Password;
                var certificateValidation = ElasticCertificateTrust.CreateValidationCallback(endpoint);

                var bootstrap = Environment.GetEnvironmentVariable("ELASTIC_BOOTSTRAP_METHOD")?.Trim();
                if (string.IsNullOrEmpty(bootstrap)) bootstrap = "Silent";
                bool registerTemplate = !string.Equals(bootstrap, "None", StringComparison.OrdinalIgnoreCase);
                bool failOnTemplateError = string.Equals(bootstrap, "Failure", StringComparison.OrdinalIgnoreCase);
                if (registerTemplate && !failOnTemplateError && !string.Equals(bootstrap, "Silent", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("ELASTIC_BOOTSTRAP_METHOD must be None, Silent or Failure.");

                var loggerConfiguration = new LoggerConfiguration().MinimumLevel.Verbose();
                FormatIndexName(indexFormat, DateTimeOffset.UtcNow);
                var options = new Serilog.Sinks.Elasticsearch.ElasticsearchSinkOptions(endpoint)
                {
                    IndexFormat = indexFormat,
                    InlineFields = false,
                    TypeName = null,
                    DetectElasticsearchVersion = true,
                    AutoRegisterTemplate = registerTemplate,
                    TemplateName = "dmicroservices-" + ToIndexSearchTarget(indexFormat).Replace('*', 'x'),
                    RegisterTemplateFailure = failOnTemplateError
                        ? Serilog.Sinks.Elasticsearch.RegisterTemplateRecovery.FailSink
                        : Serilog.Sinks.Elasticsearch.RegisterTemplateRecovery.IndexAnyway,
                    ModifyConnectionSettings = settings =>
                    {
                        settings = settings.EnableApiVersioningHeader();
                        if (certificateValidation != null) settings = settings.ServerCertificateValidationCallback(certificateValidation);
                        return string.IsNullOrEmpty(username) ? settings : settings.BasicAuthentication(username, password);
                    },
                    EmitEventFailure = Serilog.Sinks.Elasticsearch.EmitEventFailureHandling.RaiseCallback,
                    FailureCallback = logEvent => WriteToConsoleFallback(indexFormat, logEvent)
                };
                Serilog.LoggerConfigurationElasticsearchExtensions.Elasticsearch(loggerConfiguration.WriteTo, options);

                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POD_NAME")))
                    loggerConfiguration.Enrich.WithProperty("PodName", Environment.GetEnvironmentVariable("POD_NAME"));

                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HOSTNAME")))
                    loggerConfiguration.Enrich.WithProperty("PodId", Environment.GetEnvironmentVariable("HOSTNAME"));

                logger = loggerConfiguration.CreateLogger();
            }
            catch (Exception error)
            {
                logger = null;
                ReportLoggerFailure("Elasticsearch sink setup", error);
            }
        }

        internal static void ReportLoggerFailure(string operation, Exception error)
        {
            try
            {
                var details = new List<string>();
                for (var current = error; current != null && details.Count < 8; current = current.InnerException)
                    details.Add(current.GetType().Name + ": " + current.Message);
                string message = string.Join(" --> ", details);
                Console.Error.WriteLine($"DMicroservices ElasticLogger: {operation} failed ({error.GetType().Name}): {RedactSecrets(message)}");
            }
            catch {}
        }

       
        private static string RedactSecrets(string message, bool maskUserName = true)
        {
            if (string.IsNullOrEmpty(message)) return message;
            var secrets = new List<string> { Environment.GetEnvironmentVariable("ELASTIC_PASSWORD") };
            if (maskUserName)
            {
                secrets.Add(ElasticUri);
                secrets.Add(Environment.GetEnvironmentVariable("ELASTIC_USERNAME"));
            }
            if (Uri.TryCreate(ElasticUri, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.UserInfo))
            {
                secrets.Add(ElasticUri);
                secrets.Add(uri.AbsoluteUri);
                secrets.Add(uri.ToString());
                secrets.Add(uri.UserInfo);
                secrets.Add(Uri.UnescapeDataString(uri.UserInfo));
                int separator = uri.UserInfo.IndexOf(':');
                if (separator >= 0)
                {
                    if (maskUserName)
                    {
                        secrets.Add(uri.UserInfo.Substring(0, separator));
                        secrets.Add(Uri.UnescapeDataString(uri.UserInfo.Substring(0, separator)));
                    }
                    secrets.Add(uri.UserInfo.Substring(separator + 1));
                    secrets.Add(Uri.UnescapeDataString(uri.UserInfo.Substring(separator + 1)));
                    secrets.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(Uri.UnescapeDataString(uri.UserInfo))));
                }
            }
            string envUser = Environment.GetEnvironmentVariable("ELASTIC_USERNAME"), envPassword = Environment.GetEnvironmentVariable("ELASTIC_PASSWORD");
            if (!string.IsNullOrEmpty(envUser) && !string.IsNullOrEmpty(envPassword))
                secrets.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(envUser + ":" + envPassword)));
            foreach (string secret in secrets.Where(x => !string.IsNullOrEmpty(x)).OrderByDescending(x => x.Length))
            {
                message = message.Replace(secret, "[REDACTED]");
                message = message.Replace(Uri.EscapeDataString(secret), "[REDACTED]");
            }
            return message;
        }

        private static Serilog.Core.Logger CreateConsoleFallbackLogger(string route) =>
            new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(new ConsoleFallbackSink(route)).CreateLogger();

        internal static void WriteToConsoleFallback(string route, Serilog.Events.LogEvent logEvent)
        {
            if (logEvent == null) return;
            try
            {
                string index = route;
                try { index = FormatIndexName(route, logEvent.Timestamp); } catch (ArgumentException) { }
                var line = new StringBuilder()
                    .Append(logEvent.Timestamp.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture))
                    .Append(" [").Append(logEvent.Level).Append("] [ELASTIC-FALLBACK ").Append(index).Append("] ")
                    .Append(logEvent.RenderMessage(CultureInfo.InvariantCulture));
                if (logEvent.Exception != null)
                    line.Append(" | Exception: ").Append(logEvent.Exception);
                string text = line.ToString().Replace("\r\n", " | ").Replace("\n", " | ").Replace("\r", " ");
                Console.Out.WriteLine(RedactSecrets(text, maskUserName: false));
            }
            catch {  }
        }

        private sealed class ConsoleFallbackSink : Serilog.Core.ILogEventSink
        {
            private readonly string _route;
            public ConsoleFallbackSink(string route) { _route = route; }
            public void Emit(Serilog.Events.LogEvent logEvent) => WriteToConsoleFallback(_route, logEvent);
        }

        public static string FormatIndexName(string route, DateTimeOffset timestamp)
        {
            if (string.IsNullOrWhiteSpace(route)) throw new ArgumentException("Log index cannot be empty.", nameof(route));
            string name;
            try { name = string.Format(CultureInfo.InvariantCulture, route, timestamp.ToUniversalTime()).ToLowerInvariant(); }
            catch (FormatException error) { throw new ArgumentException("Invalid LOG_INDEX_FORMAT date placeholder.", nameof(route), error); }
            if (name.Length > 255 || !System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-z0-9][a-z0-9._-]*$")
                || name == "." || name == "..")
                throw new ArgumentException("Log index contains invalid characters or exceeds 255 characters.", nameof(route));
            return name;
        }

        internal static string ToIndexSearchTarget(string route)
        {
            FormatIndexName(route, DateTimeOffset.UtcNow);
            return System.Text.RegularExpressions.Regex.Replace(route.ToLowerInvariant(), @"\{0:[^{}]+\}", "*");
        }

        public static string GetLogSearchTarget(string route) => ToIndexSearchTarget(route);

        public void CloseAndFlush()
        {
            _lifecycle.EnterWriteLock();
            try
            {
                if (_closed) return;
                _closed = true;
                _configured = false;
                var loggers = new List<Serilog.Core.Logger> { _errorLogger, _infoLogger, _consoleErrorLogger, _consoleInfoLogger };
                loggers.AddRange(SpecificIndexFormat.Values.Select(x => x.Item2));
                loggers.AddRange(SpecificIndexFileFormat.Values.Select(x => x.Item2));
                _errorLogger = null;
                _infoLogger = null;
                _consoleErrorLogger = null;
                _consoleInfoLogger = null;
                SpecificIndexFormat.Clear();
                SpecificIndexFileFormat.Clear();
                foreach (var logger in loggers.Where(x => x != null).Distinct())
                {
                    try { logger.Dispose(); } catch (Exception error) { ReportLoggerFailure("sink shutdown", error); }
                }
            }
            finally { _lifecycle.ExitWriteLock(); }
        }

        private void SetStacktraceExceptionSource(Exception ex)
        {
            ex.Source = string.IsNullOrEmpty(ex.Source) ? Environment.StackTrace : ex.Source + "---------StackTrace---------" + Environment.StackTrace;
            ex.Source = ex.Source.Replace("at System.Environment.get_StackTrace()" + Environment.NewLine, string.Empty);
        }
        private Serilog.Core.Logger GetSpecificLoggerFileInstance(string fileName, string logFilePath, bool outputControl)
        {
            if (SpecificIndexFileFormat.TryGetValue(fileName, out var specificLoggerItems) && specificLoggerItems.Item1)
            {
                return specificLoggerItems.Item2;
            }
            else
            {
                lock (_objectInitializeLock)
                {
                    if (SpecificIndexFileFormat.TryGetValue(fileName,
                            out specificLoggerItems) && specificLoggerItems.Item1)
                    {
                        return specificLoggerItems.Item2;
                    }

                    string outputTemplate = string.Empty;

                    if (outputControl)
                        outputTemplate = "-----------------------------------{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}-----------------------------------{NewLine}{Exception}-----------------------------------LOG END LINE-----------------------------------{NewLine}{NewLine}";
                    else
                        outputTemplate = "-----------------------------------{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}-----------------------------------{NewLine}{NewLine}";

                    Serilog.Core.Logger specificLoggerInstance = null;
                    ConfigureFileLogger(fileName, outputTemplate, ref specificLoggerInstance, logFilePath);
                    if (specificLoggerInstance == null)
                        specificLoggerInstance = CreateConsoleFallbackLogger(fileName);
                    SpecificIndexFileFormat.TryAdd(fileName, Tuple.Create(true, specificLoggerInstance));
                    return specificLoggerInstance;
                }
            }
        }

        private string CreateMessageTemplate(string messageTemplate)
        {
            if (messageTemplate == null)
                messageTemplate = $"Parent: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}";
            else
                messageTemplate += $", Parent: {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}";
            return messageTemplate;
        }

        private string CreateWithParameterMessageTemplate(string messageTemplate, Dictionary<string, object> parameters)
        {
            StringBuilder stringBuilder = new StringBuilder(messageTemplate);

            foreach (var parameter in parameters)
            {
                stringBuilder.Append("{");
                stringBuilder.Append(parameter.Key);
                stringBuilder.AppendLine("}");
            }
            return stringBuilder.ToString();
        }
    }
}