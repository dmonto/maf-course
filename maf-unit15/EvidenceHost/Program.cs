using System.Diagnostics;
using System.Text.Json.Serialization;
using Maf.Unit15.Application;
using Maf.Unit15.Infrastructure;

EnvironmentFile.Load();
var builder = WebApplication.CreateBuilder(args);
string mode = builder.Configuration["mode"] ?? builder.Configuration["Runner:Mode"] ?? "fixture";
if (mode is not ("fixture" or "azure")) throw new InvalidOperationException("Host mode must be fixture or azure.");
string url = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://127.0.0.1:8082";
if (url is not ("http://127.0.0.1:8082" or "http://127.0.0.1:8080")) throw new InvalidOperationException("Use a loopback host URL on port 8080 or 8082.");
builder.WebHost.UseUrls(url);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 4096);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
IEvidenceReader reader = mode == "azure" ? new MafEvidenceReader() : new FixtureEvidenceReader();
builder.Services.AddSingleton(reader);
int concurrency = builder.Configuration.GetValue<int?>("Runner:Concurrency") ?? 2;
if (concurrency is < 1 or > 32) throw new InvalidOperationException("Runner__Concurrency must be 1..32.");
using var capacity = new SemaphoreSlim(concurrency, concurrency);
var app = builder.Build();
LabConsole.Write("HOST", $"Evidence host: mode={mode}, listening on {url}, reader={reader.GetType().Name}. Routes: GET /health/live, GET /health/ready, POST /api/status");
string release = builder.Configuration["Runner:Release"] ?? "15.2.0";
app.MapGet("/health/live", () => new { status = "ok", mode, release });
app.MapGet("/health/ready", () => new { status = "ok", mode, release });
app.MapPost("/api/status", async Task<IResult> (StatusRequest request, HttpContext http, IEvidenceReader evidence) =>
{
    string service = request.Service?.Trim().ToLowerInvariant() ?? "";
    LabConsole.Write("HOST REQUEST", $"POST /api/status service=\"{service}\"");
    if (service is not ("vpn" or "email")) return Results.BadRequest(new { error = "unknown_service" });
    if (!await capacity.WaitAsync(0, http.RequestAborted))
    {
        http.Response.Headers.RetryAfter = "1";
        return Results.Json(new { error = "busy" }, statusCode: 429);
    }
    using var budget = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted, app.Lifetime.ApplicationStopping);
    budget.CancelAfter(TimeSpan.FromSeconds(20));
    string requestId = Guid.NewGuid().ToString("N");
    try
    {
        Evidence result = await evidence.ReadAsync(service, budget.Token);
        LabConsole.Write("HOST RESPONSE", $"200 {result.Service}/{result.Status} (mode={mode})");
        return Results.Json(new { requestId, traceId = Activity.Current?.TraceId.ToString(), mode, release,
            result.Service, result.Status, summary = StatusTools.Catalog(service).Summary });
    }
    catch (OperationCanceledException) { return Results.Json(new { requestId, error = "request_cancelled" }, statusCode: 504); }
    catch (Exception error)
    {
        app.Logger.LogWarning("Evidence failed {RequestId} {FailureType}", requestId, error.GetType().Name);
        return Results.Json(new { requestId, error = "evidence_unavailable" }, statusCode: 502);
    }
    finally { capacity.Release(); }
});
await app.RunAsync();
internal sealed record StatusRequest(string? Service);
