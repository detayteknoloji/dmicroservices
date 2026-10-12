using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using DMicroservices.DataAccess.ElasticSearch;
using DMicroservices.Utils.Logger;

namespace DMicroservices.Elastic.Smoke;

internal static class CompatibilityChecks
{
    public static HttpClient CreateHttpClient()
    {
        var connection = ElasticConnectionSettings.FromEnvironment(Environment.GetEnvironmentVariable("ELASTIC_URI"));
        var handler = new HttpClientHandler();
        var validate = ElasticCertificateTrust.CreateValidationCallback(connection.Endpoint);
        if (validate != null)
            handler.ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) =>
                validate(request, certificate, chain, errors);
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        if (!string.IsNullOrEmpty(connection.Username))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(connection.Username + ":" + connection.Password)));
        return http;
    }

    public static async Task WaitForExpectedVersionAsync()
    {
        string expected = Environment.GetEnvironmentVariable("COMPAT_EXPECTED_ELASTIC");
        if (string.IsNullOrWhiteSpace(expected)) return;
        var connection = ElasticConnectionSettings.FromEnvironment(Environment.GetEnvironmentVariable("ELASTIC_URI"));
        using var http = CreateHttpClient();
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await http.GetAsync(connection.Endpoint);
                if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403)
                    throw new InvalidOperationException("Elasticsearch compatibility authentication failed: HTTP " + (int)response.StatusCode);
                if (response.IsSuccessStatusCode)
                {
                    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    string actual = document.RootElement.GetProperty("version").GetProperty("number").GetString();
                    if (actual != expected)
                        throw new InvalidOperationException($"Expected Elasticsearch {expected}; server reports {actual}.");
                    Console.WriteLine("PASS VERSION: Elasticsearch " + actual);
                    return;
                }
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            await Task.Delay(1000);
        }
        throw new TimeoutException("Expected Elasticsearch service did not become ready.");
    }

    public static async Task VerifyHelperAsync()
    {
        if (Environment.GetEnvironmentVariable("COMPAT_VERIFY_HELPER") != "1") return;
        string index = "compat-helper-" + Guid.NewGuid().ToString("N");
        string marker = Guid.NewGuid().ToString("N");
        var helper = ElasticHelper<CompatibilityDocument>.Instance;
        var indexed = await helper.AddIndexAsync(new CompatibilityDocument { Name = marker }, index);
        if (!indexed.IsValid) throw new InvalidOperationException("ElasticHelper write failed.");
        var connection = ElasticConnectionSettings.FromEnvironment(Environment.GetEnvironmentVariable("ELASTIC_URI"));
        using var http = CreateHttpClient();
        using var refresh = await http.PostAsync(connection.Endpoint.AbsoluteUri.TrimEnd('/') + "/" + index + "/_refresh", null);
        refresh.EnsureSuccessStatusCode();
        var searched = helper.Search(query => query.MatchAll(), index);
        if (!searched.IsValid || searched.Documents.Count != 1 || searched.Documents.Single().Name != marker)
            throw new InvalidOperationException("ElasticHelper search failed.");
        Console.WriteLine("PASS ELASTIC HELPER: package NEST write/read over configured authenticated connection.");
    }

    public static void PrepareFixture(string directory)
    {
        Directory.CreateDirectory(directory);
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest("CN=Disposable Elastic Compatibility CA", rootKey,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var ca = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(3));
        using var leafKey = RSA.Create(2048);
        var leafRequest = new CertificateRequest("CN=elasticsearch", leafKey,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        leafRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection
        { new Oid("1.3.6.1.5.5.7.3.1"), new Oid("1.3.6.1.5.5.7.3.2") }, true));
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("elasticsearch");
        names.AddDnsName("localhost");
        names.AddIpAddress(System.Net.IPAddress.Loopback);
        leafRequest.CertificateExtensions.Add(names.Build());
        var serial = new byte[16];
        RandomNumberGenerator.Fill(serial);
        using var issued = leafRequest.Create(ca, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(2), serial);
        using var leaf = issued.CopyWithPrivateKey(leafKey);
        using var publicCa = new X509Certificate2(ca.RawData);
        File.WriteAllBytes(Path.Combine(directory, "elastic.p12"),
            new X509Certificate2Collection { leaf, publicCa }.Export(X509ContentType.Pfx, "compat-p12"));
        File.WriteAllText(Path.Combine(directory, "ca.crt"), "-----BEGIN CERTIFICATE-----\n" +
            Convert.ToBase64String(ca.RawData, Base64FormattingOptions.InsertLineBreaks) + "\n-----END CERTIFICATE-----\n",
            new UTF8Encoding(false));
        Console.WriteLine("Prepared disposable TLS fixture: " + directory);
    }
}

public sealed class CompatibilityDocument
{
    public string Name { get; set; }
}
