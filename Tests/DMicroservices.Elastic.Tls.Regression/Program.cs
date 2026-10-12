using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DMicroservices.Utils.Logger;

Console.WriteLine("RUNTIME: " + Environment.Version);
if (Environment.Version.Major != 5) throw new InvalidOperationException("This probe requires .NET 5.");

// Run in a child process so CA environment and certificate caches never affect other tests.
var previousFile = Environment.GetEnvironmentVariable("SSL_CERT_FILE");
var previousDirectory = Environment.GetEnvironmentVariable("SSL_CERT_DIR");
var directory = Path.Combine(Path.GetTempPath(), "elastic-tls-regression-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
using var caKey = RSA.Create(2048);
var caRequest = new CertificateRequest("CN=Local Elastic Test CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
using var ca = caRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(2));
string caPath = Path.Combine(directory, "ca.crt");
File.WriteAllText(caPath, "-----BEGIN CERTIFICATE-----\n" + Convert.ToBase64String(ca.RawData, Base64FormattingOptions.InsertLineBreaks) + "\n-----END CERTIFICATE-----\n");
Environment.SetEnvironmentVariable("ELASTIC_URI_SSL", caPath);
var validate = ElasticCertificateTrust.CreateValidationCallback(new Uri("https://localhost:9200"));
if (validate == null) throw new Exception("CA callback missing.");

byte[] SerialNumber() { var serial = new byte[16]; RandomNumberGenerator.Fill(serial); return serial; }
X509Certificate2 Leaf(bool expired = false, bool clientOnly = false)
{
    using var key = RSA.Create(2048);
    var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
    request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection
        { new Oid(clientOnly ? "1.3.6.1.5.5.7.3.2" : "1.3.6.1.5.5.7.3.1") }, true));
    var names = new SubjectAlternativeNameBuilder(); names.AddDnsName("localhost");
    request.CertificateExtensions.Add(names.Build());
    return request.Create(ca, DateTimeOffset.UtcNow.AddDays(-1),
        expired ? DateTimeOffset.UtcNow.AddHours(-1) : DateTimeOffset.UtcNow.AddDays(1), SerialNumber());
}
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
using var leaf = Leaf();
Check(validate(null, leaf, null, SslPolicyErrors.RemoteCertificateChainErrors), "Configured CA rejected.");
Check(validate(null, leaf, null, SslPolicyErrors.None), "Existing platform trust rejected.");
Check(!validate(null, leaf, null, SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors), "Hostname error accepted.");
Check(!validate(null, null, null, SslPolicyErrors.RemoteCertificateNotAvailable), "Missing certificate accepted.");
using var expired = Leaf(expired: true);
Check(!validate(null, expired, null, SslPolicyErrors.RemoteCertificateChainErrors), "Expired certificate accepted.");
using var clientOnly = Leaf(clientOnly: true);
Check(!validate(null, clientOnly, null, SslPolicyErrors.RemoteCertificateChainErrors), "Client-only certificate accepted as server.");
using var otherKey = RSA.Create(2048);
var otherRequest = new CertificateRequest("CN=Unrelated CA", otherKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
otherRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
using var unrelated = otherRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
Check(!validate(null, unrelated, null, SslPolicyErrors.RemoteCertificateChainErrors), "Unrelated CA accepted.");
using var platform = new X509Chain();
platform.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
platform.ChainPolicy.DisableCertificateDownloads = true;
Check(!platform.Build(leaf), "Private CA leaked into global trust.");
Check(ElasticCertificateTrust.CreateValidationCallback(new Uri("http://localhost:9200")) == null, "HTTP must not install a TLS callback.");
Check(Environment.GetEnvironmentVariable("SSL_CERT_FILE") == previousFile &&
    Environment.GetEnvironmentVariable("SSL_CERT_DIR") == previousDirectory, "Platform trust environment changed.");
Console.WriteLine("PASS: private CA, platform trust, hostname errors, expiry, server usage and unrelated CA checks.");
Console.WriteLine("Temporary public certificate fixture: " + directory);
