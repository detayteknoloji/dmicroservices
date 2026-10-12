using System;
using System.Collections.Concurrent;

namespace DMicroservices.Utils.Logger
{
    public sealed class ElasticConnectionSettings
    {
        private static readonly ConcurrentDictionary<string, bool> ReportedWarnings = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        public Uri Endpoint { get; }
        public string Username { get; }
        public string Password { get; }

        private ElasticConnectionSettings(Uri endpoint, string username, string password)
        {
            Endpoint = endpoint;
            Username = username;
            Password = password;
        }

        public static ElasticConnectionSettings FromEnvironment(string address) => Resolve(address,
            Environment.GetEnvironmentVariable("ELASTIC_USERNAME"), Environment.GetEnvironmentVariable("ELASTIC_PASSWORD"));

        public static ElasticConnectionSettings Resolve(string address, string username, string password)
        {
            var uri = new Uri(address);
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException("Elasticsearch endpoint must be an HTTP(S) URI.");

            string uriUsername = null, uriPassword = null;
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                int separator = uri.UserInfo.IndexOf(':');
                uriUsername = Uri.UnescapeDataString(separator < 0 ? uri.UserInfo : uri.UserInfo.Substring(0, separator));
                uriPassword = separator < 0 ? string.Empty : Uri.UnescapeDataString(uri.UserInfo.Substring(separator + 1));
            }

            bool hasUsername = !string.IsNullOrWhiteSpace(username), hasPassword = !string.IsNullOrEmpty(password);
            if (hasUsername != hasPassword)
                Warn("ELASTIC_USERNAME ve ELASTIC_PASSWORD birlikte saðlanmalýdýr; kýsmi çift yok sayýlýyor.");
            if (!hasUsername || !hasPassword) { username = uriUsername; password = uriPassword; }
            if (!string.IsNullOrEmpty(username) && uri.Scheme != Uri.UriSchemeHttps)
                Warn("Elasticsearch Temel Kimlik Doðrulamasý HTTP üzerinden kimlik bilgilerini þifrelenmemiþ olarak gönderir; HTTPS kullanýn.");

            var endpoint = new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty }.Uri;
            return new ElasticConnectionSettings(endpoint, username, password);
        }

        private static void Warn(string message)
        {
            if (ReportedWarnings.TryAdd(message, true))
                ElasticLogger.ReportLoggerFailure("Elasticsearch connection settings", new ArgumentException(message));
        }

        public override string ToString() => Endpoint.ToString();
    }
}
