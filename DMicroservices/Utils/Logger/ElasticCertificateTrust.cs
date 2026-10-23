using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace DMicroservices.Utils.Logger
{
    /// <summary>Elasticsearch baðlantýsýna özel CA ekler</summary>
    public static class ElasticCertificateTrust
    {
        public const string DefaultCertificatePath = "/etc/elastic/ca.crt";
        private static readonly ConcurrentDictionary<string, Lazy<X509Certificate2[]>> Certificates
            = new ConcurrentDictionary<string, Lazy<X509Certificate2[]>>(StringComparer.Ordinal);

        public static Func<object, X509Certificate, X509Chain, SslPolicyErrors, bool> CreateValidationCallback(Uri endpoint)
        {
            if (endpoint == null || endpoint.Scheme != Uri.UriSchemeHttps) return null;
            var configured = Environment.GetEnvironmentVariable("ELASTIC_URI_SSL");
            var path = string.IsNullOrWhiteSpace(configured) ? DefaultCertificatePath : configured.Trim();
            if (!File.Exists(path))
            {
                if (string.IsNullOrWhiteSpace(configured)) return null; // http ise ca ekleme
                if (!File.Exists(DefaultCertificatePath))
                {
                    // bulamazsak default devam et
                    ElasticLogger.ReportLoggerFailure("Elasticsearch CA path", new FileNotFoundException("ELASTIC_URI_SSL and /etc/elastic/ca.crt are missing; using platform trust."));
                    return null;
                }
                path = DefaultCertificatePath;
                ElasticLogger.ReportLoggerFailure("Elasticsearch CA path", new FileNotFoundException("ELASTIC_URI_SSL file missing; using /etc/elastic/ca.crt."));
            }
            X509Certificate2[] certificates;
            try
            {
                certificates = Certificates.GetOrAdd(Path.GetFullPath(path), p =>
                    new Lazy<X509Certificate2[]>(() => ReadAuthorities(p))).Value;
            }
            catch (Exception error) when (error is ArgumentException || error is CryptographicException || error is IOException
                || error is FormatException || error is UnauthorizedAccessException)
            {
                ElasticLogger.ReportLoggerFailure("Elasticsearch CA file", error);
                return null;
            }
            return (sender, certificate, chain, errors) => Validate(certificate, chain, errors, certificates);
        }

        private static X509Certificate2[] ReadAuthorities(string path)
        {
            var pem = File.ReadAllText(path);
            var blocks = Regex.Matches(pem, @"-----BEGIN CERTIFICATE-----\s*(.*?)\s*-----END CERTIFICATE-----", RegexOptions.Singleline);
            var authorities = blocks.Cast<Match>().Select(block =>
                new X509Certificate2(Convert.FromBase64String(block.Groups[1].Value))).ToArray();
            if (authorities.Length == 0 || authorities.Any(c => !c.Extensions.OfType<X509BasicConstraintsExtension>().Any(x => x.CertificateAuthority)))
            {
                foreach (var certificate in authorities) certificate.Dispose();
                throw new ArgumentException("ELASTIC_URI_SSL must contain PEM CA certificates, not a server certificate or private key.");
            }
            return authorities;
        }

        private static bool Validate(X509Certificate certificate, X509Chain suppliedChain, SslPolicyErrors errors, X509Certificate2[] authorities)
        {
            if (certificate == null) return false;
            if (errors == SslPolicyErrors.None) return true;
            if (errors != SslPolicyErrors.RemoteCertificateChainErrors) return false;
            const X509ChainStatusFlags trustErrors = X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.PartialChain;
            if (suppliedChain != null && suppliedChain.ChainStatus.Any(x => (x.Status & ~trustErrors) != 0)) return false;
            try
            {
                using (var leaf = new X509Certificate2(certificate))
                using (var chain = new X509Chain())
                {
                    chain.ChainPolicy.ExtraStore.AddRange(authorities);
                    if (suppliedChain != null)
                        foreach (var element in suppliedChain.ChainElements.Cast<X509ChainElement>().Skip(1))
                            chain.ChainPolicy.ExtraStore.Add(element.Certificate);
                    chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                    if (!chain.Build(leaf) || chain.ChainElements.Count == 0
                        || chain.ChainStatus.Any(x => (x.Status & ~trustErrors) != 0)) return false;
                    var root = chain.ChainElements[chain.ChainElements.Count - 1].Certificate.RawData;
                    return authorities.Any(c => root.SequenceEqual(c.RawData));
                }
            }
            catch (CryptographicException) { return false; }
        }
    }
}
