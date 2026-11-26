using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 4096);
builder.Services.Configure<HostOptions>(o =>
    o.ShutdownTimeout = TimeSpan.FromSeconds(30));

string Required(string key) =>
    builder.Configuration[key] is { Length: > 0 } value && !value.Contains('<')
        ? value : throw new InvalidOperationException($"Missing {key}");

string mode = builder.Configuration["Runner:Mode"] ?? "fixture";
if (mode is not ("fixture" or "azure"))
    throw new InvalidOperationException("Runner:Mode must be fixture or azure.");
string release = builder.Configuration["Runner:Release"] ?? "local";
const string promptVersion = "status-v1";
string instance = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")
    ?? Environment.GetEnvironmentVariable("HOSTNAME")
    ?? Environment.MachineName;
int concurrency = builder.Configuration.GetValue<int?>("Runner:Concurrency") ?? 2;
if (concurrency is < 1 or > 32)
    throw new InvalidOperationException("Runner:Concurrency must be 1..32.");
using var capacity = new SemaphoreSlim(concurrency, concurrency);

AzureOpenAIClient? model = null;
string deployment = "none";
if (mode == "azure")
{
    var endpoint = new Uri(Required("AZURE_OPENAI_ENDPOINT"));
    if (endpoint.Scheme != "https" || !endpoint.IsDefaultPort ||
        endpoint.UserInfo.Length > 0 || endpoint.AbsolutePath != "/" ||
        endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0 ||
        !endpoint.Host.EndsWith(".openai.azure.com", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Use the assigned Azure OpenAI endpoint.");
    deployment = Required("AZURE_OPENAI_DEPLOYMENT_NAME");
    TokenCredential credential = Required("Runner:Identity") switch
    {
        "cli" => new AzureCliCredential(),
        "managed" => new ManagedIdentityCredential(),
        "workload" => new WorkloadIdentityCredential(),
        _ => throw new InvalidOperationException("Identity must be cli, managed or workload.")
    };
    model = new AzureOpenAIClient(endpoint, credential);
}

var app = builder.Build();
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

object Health() => new { status = "ok", mode, release, promptVersion, instance };
app.MapGet("/health/live", () => Results.Json(Health()));
// Startup configuration is validated above. This endpoint does not call the model.
app.MapGet("/health/ready", () => Results.Json(Health()));

app.MapPost("/api/status", async Task<IResult> (StatusRequest request, HttpContext http) =>
{
    string service = request.Service?.Trim().ToLowerInvariant() ?? "";
    if (!Catalog.Items.ContainsKey(service))
        return Results.BadRequest(new { error = "unknown_service" });
    if (!await capacity.WaitAsync(0, http.RequestAborted))
    {
        http.Response.Headers["Retry-After"] = "1";
        return Results.Json(new { error = "busy" }, statusCode: 429);
    }

    string requestId = Guid.NewGuid().ToString("N");
    var watch = Stopwatch.StartNew();
    int outcome = 200;
    using var budget = CancellationTokenSource.CreateLinkedTokenSource(
        http.RequestAborted, app.Lifetime.ApplicationStopping);
    budget.CancelAfter(TimeSpan.FromSeconds(20));
    try
    {
        var tools = new StatusTools(service);
        Evidence answer;
        if (model is null)
        {
            await Task.Delay(50, budget.Token);
            answer = tools.GetServiceStatus(service);
        }
        else
        {
            AIAgent agent = model.GetChatClient(deployment).AsAIAgent(
                new ChatClientAgentOptions
                {
                    Name = "portable-service-status",
                    ChatOptions = new ChatOptions
                    {
                        Instructions = """
                            Call GetServiceStatus for the requested service.
                            Return only JSON with service, status and summary.
                            Copy all three values exactly from the tool result.
                            Never claim a repair, notification or another write action.
                            """,
                        MaxOutputTokens = 250,
                        ResponseFormat = ChatResponseFormat.Json,
                        Tools = [AIFunctionFactory.Create(tools.GetServiceStatus)]
                    }
                },
                clientFactory: inner => new FunctionInvokingChatClient(inner)
                    { MaximumIterationsPerRequest = 3 });
            string raw = (await agent.RunAsync($"Requested service: {service}",
                cancellationToken: budget.Token)).Text;
            answer = JsonSerializer.Deserialize<Evidence>(raw, json)
                ?? throw new InvalidOperationException("Empty model contract.");
        }

        if (tools.Calls == 0 || answer != Catalog.Items[service])
            throw new InvalidOperationException("Evidence contract failed.");
        return Results.Json(new { requestId, mode, release, promptVersion, instance,
            answer.Service, answer.Status, answer.Summary });
    }
    catch (OperationCanceledException)
    {
        outcome = 504;
        return Results.Json(new { requestId, error = "request_cancelled" }, statusCode: outcome);
    }
    catch (Exception error)
    {
        outcome = 502;
        app.Logger.LogWarning("Agent failure {RequestId} {FailureType}",
            requestId, error.GetType().Name);
        return Results.Json(new { requestId, error = "agent_unavailable" }, statusCode: outcome);
    }
    finally
    {
        capacity.Release();
        app.Logger.LogInformation(
            "Status completed {RequestId} {Service} {Outcome} {ElapsedMs} {Release} {Mode} {Instance}",
            requestId, service, outcome, watch.ElapsedMilliseconds, release, mode, instance);
    }
});

app.Run();

internal sealed record StatusRequest(string? Service);
internal sealed record Evidence(string Service, string Status, string Summary);
internal static class Catalog
{
    public static readonly IReadOnlyDictionary<string, Evidence> Items =
        new Dictionary<string, Evidence>
        {
            ["vpn"] = new("vpn", "degraded", "Synthetic evidence: VPN connectivity is degraded."),
            ["email"] = new("email", "healthy", "Synthetic evidence: email is healthy.")
        };
}
internal sealed class StatusTools(string selectedService)
{
    public int Calls { get; private set; }

    [Description("Read synthetic status for the request-bound service.")]
    public Evidence GetServiceStatus(
        [Description("The selected service: vpn or email.")] string service)
    {
        if (service != selectedService)
            throw new InvalidOperationException("Service scope mismatch.");
        Calls++;
        return Catalog.Items[service];
    }
}
