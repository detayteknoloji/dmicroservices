using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace DMicroservices.Elastic.TestSupport;

internal sealed class ElasticHttpFixture : IAsyncDisposable
{
    private readonly IHost application;
    private readonly ConcurrentQueue<(string Index, JsonElement Document)> records = new();
    public ConcurrentQueue<(string Method, string Path)> Requests { get; } = new();
    public ConcurrentQueue<string> ContractErrors { get; } = new();
    public bool DenyTemplates { get; set; }
    public bool RejectItems { get; set; }
    public int BulkStatus { get; set; } = 200;
    public string Address { get; private set; }
    public (string Index, JsonElement Document)[] Records => records.ToArray();

    private ElasticHttpFixture(IHost application) { this.application = application; }

    public static async Task<ElasticHttpFixture> StartAsync()
    {
        ElasticHttpFixture fixture = null;
        var application = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureWebHostDefaults(web => web
                .UseKestrel(options => options.Listen(IPAddress.Loopback, 0))
                .Configure(app => app.Run(context => fixture.HandleAsync(context))))
            .Build();
        fixture = new ElasticHttpFixture(application);
        await application.StartAsync();
        fixture.Address = application.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>().Addresses.Single();
        return fixture;
    }

    private async Task HandleAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        Requests.Enqueue((context.Request.Method, path));
        context.Response.Headers["X-Elastic-Product"] = "Elasticsearch";
        bool template = path.Contains("/_template/") || path.Contains("/_index_template/") || path.Contains("/_component_template/");
        if (context.Request.Method == "HEAD")
        {
            context.Response.StatusCode = template && DenyTemplates ? 404 : 200;
            return;
        }
        if (template && DenyTemplates)
        {
            context.Response.StatusCode = context.Request.Method == "GET" ? 404 : 403;
            await context.Response.WriteAsJsonAsync(new { error = new { type = "security_exception", reason = "fixture template registration denied" }, status = context.Response.StatusCode });
            return;
        }
        if (path == "/")
        {
            await context.Response.WriteAsJsonAsync(new { name = "local-fixture", version = new { number = "8.15.0" }, tagline = "You Know, for Search" });
            return;
        }
        if (path.EndsWith("/_bulk"))
        {
            if (!(context.Request.ContentType ?? "").Contains("compatible-with=7"))
                ContractErrors.Enqueue("Bulk compatibility header missing.");
            if (BulkStatus != 200)
            {
                context.Response.StatusCode = BulkStatus;
                await context.Response.WriteAsJsonAsync(new { error = new { type = "security_exception", reason = "fixture write denied" }, status = BulkStatus });
                return;
            }
            Stream input = context.Request.Body;
            if (context.Request.Headers["Content-Encoding"] == "gzip") input = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
            using var reader = new StreamReader(input, leaveOpen: true);
            var lines = (await reader.ReadToEndAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var items = new List<object>();
            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                using var action = JsonDocument.Parse(lines[i]);
                using var document = JsonDocument.Parse(lines[i + 1]);
                var operation = action.RootElement.EnumerateObject().Single();
                if (operation.Name != "index") ContractErrors.Enqueue("Bulk must use index operations.");
                if (operation.Value.TryGetProperty("_type", out _)) ContractErrors.Enqueue("Document types are not supported by Elasticsearch 8.");
                var index = operation.Value.GetProperty("_index").GetString();
                if (!RejectItems) records.Enqueue((index, document.RootElement.Clone()));
                items.Add(new Dictionary<string, object> { [operation.Name] = new
                {
                    status = RejectItems ? 400 : 201, _index = index, _id = "fixture",
                    error = RejectItems ? new { type = "mapper_parsing_exception", reason = "fixture rejection" } : null
                } });
            }
            await context.Response.WriteAsJsonAsync(new { errors = RejectItems, items });
            return;
        }
        if (path.EndsWith("/_search"))
        {
            using var query = await JsonDocument.ParseAsync(context.Request.Body);
            var conditions = query.RootElement.GetProperty("query").GetProperty("bool").GetProperty("must")
                .EnumerateArray().Select(condition => condition.GetProperty("bool").GetProperty("should")[0]
                    .GetProperty("term").EnumerateObject().Single()).ToArray();
            string pattern = "^" + Regex.Escape(Uri.UnescapeDataString(path.Split('/')[1])).Replace("\\*", ".*") + "$";
            int count = Records.Count(record => Regex.IsMatch(record.Index, pattern) && conditions.All(condition =>
            {
                string field = condition.Name.Split('.')[1];
                return record.Document.TryGetProperty("fields", out var fields) && fields.TryGetProperty(field, out var value)
                    && value.GetString() == condition.Value.GetString();
            }));
            await context.Response.WriteAsJsonAsync(new { hits = new { total = new { value = count, relation = "eq" } } });
            return;
        }
        await context.Response.WriteAsJsonAsync(new { acknowledged = true });
    }

    public async ValueTask DisposeAsync()
    {
        await application.StopAsync();
        application.Dispose();
    }
}
