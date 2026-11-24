using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;

namespace Unit14;

public sealed record ApiSettings(string Mode, string Release, int Concurrency, string Identity,
    int FixtureDelayMs = 50, bool WrongFixtureContract = false)
{
    public int RequestTimeoutSeconds { get; init; } = 90;
    public int MaxOutputTokens { get; init; } = 2048;

    private static int ReadBounded(string name, int fallback, int minimum, int maximum)
    {
        if (!int.TryParse(Lab.Value(name, fallback.ToString()), out int value) || value < minimum || value > maximum)
            throw new ArgumentException($"{name} must be {minimum}..{maximum}.");
        return value;
    }

    public static ApiSettings Read(string mode)
    {
        if (!int.TryParse(Lab.Value("Runner__Concurrency", "2"), out int limit) || limit is < 1 or > 32)
            throw new ArgumentException("Runner__Concurrency must be 1..32.");
        if (mode is not ("fixture" or "azure")) throw new ArgumentException("Use fixture or azure.");
        return new(mode, Lab.Value("Runner__Release", "14.1.0"), limit,
            Lab.Value("Runner__Identity", "cli"))
        {
            RequestTimeoutSeconds = ReadBounded("Runner__RequestTimeoutSeconds", 90, 1, 180),
            MaxOutputTokens = ReadBounded("Runner__MaxOutputTokens", 2048, 128, 8192)
        };
    }
}
public sealed record StatusRequest(string? Service);
public sealed record Evidence(string Service, string Status, string Summary);
public static class Catalog
{
    public static readonly IReadOnlyDictionary<string, Evidence> Items = new Dictionary<string, Evidence>
    {
        ["vpn"] = new("vpn", "degraded", "Synthetic evidence: VPN connectivity is degraded."),
        ["email"] = new("email", "healthy", "Synthetic evidence: email is healthy.")
    };
}
public sealed class StatusTools(string selectedService)
{
    public int Calls { get; private set; }
    [Description("Read synthetic status for the request-bound service.")]
    public Evidence GetServiceStatus([Description("The selected service: vpn or email.")] string service)
    {
        if (service != selectedService) throw new InvalidOperationException("Service scope mismatch.");
        Calls++;
        return Catalog.Items[service];
    }
}
public static class EndpointPolicy
{
    public static Uri Validate(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" ||
            !endpoint.IsDefaultPort || endpoint.UserInfo.Length > 0 || endpoint.AbsolutePath != "/" ||
            endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0 ||
            !endpoint.IdnHost.EndsWith(".openai.azure.com", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Use the assigned Azure OpenAI resource-root endpoint.");
        return endpoint;
    }
}
public static class AgentHost
{
    private sealed class RequestProgress(string initialPhase)
    {
        private string phase = initialPhase;
        public string Phase { get => Volatile.Read(ref phase); set => Volatile.Write(ref phase, value); }
    }

    private static async Task<Evidence> ExecuteAsync(ApiSettings settings, string service,
        AzureOpenAIClient? model, TokenCredential? credential, string deployment,
        RequestProgress progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tools = new StatusTools(service);
        Evidence answer;
        if (model is null)
        {
            await Task.Delay(settings.FixtureDelayMs, cancellationToken);
            answer = tools.GetServiceStatus(service);
        }
        else
        {
            // Resolve credentials separately so a deadline identifies this boundary.
            // Managed identity credentials reuse their cached token for SDK model calls.
            progress.Phase = "identity";
            _ = await credential!.GetTokenAsync(
                new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            progress.Phase = "model";
            AIAgent agent = model.GetChatClient(deployment).AsAIAgent(new ChatClientAgentOptions
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
                    MaxOutputTokens = settings.MaxOutputTokens,
                    ResponseFormat = ChatResponseFormat.Json,
                    Tools = [AIFunctionFactory.Create(tools.GetServiceStatus)]
                }
            }, clientFactory: inner => new FunctionInvokingChatClient(inner)
                { MaximumIterationsPerRequest = 3 });
            string raw = (await agent.RunAsync($"Requested service: {service}",
                cancellationToken: cancellationToken)).Text;
            cancellationToken.ThrowIfCancellationRequested();
            progress.Phase = "contract";
            answer = JsonSerializer.Deserialize<Evidence>(raw, Lab.Json)
                ?? throw new InvalidOperationException("Empty model contract.");
        }
        if (tools.Calls == 0 || answer != Catalog.Items[service])
            throw new InvalidOperationException("Evidence contract failed.");
        // Only the local recovery rehearsal can inject a deliberately bad HTTP contract.
        // The deployed API entry point never enables this option.
        if (settings.WrongFixtureContract && service == "vpn") answer = answer with { Status = "healthy" };
        return answer;
    }

    private static void ReleaseLease(CancellationTokenSource budget, SemaphoreSlim capacity)
    {
        budget.Dispose();
        try { capacity.Release(); }
        catch (ObjectDisposedException) { /* The host stopped before this operation completed. */ }
    }

    private static async Task DrainAsync(Task work, CancellationTokenSource budget, SemaphoreSlim capacity,
        Action completed, ILogger logger, string requestId)
    {
        try { await work; }
        catch (Exception error)
        {
            // Observe late failures without exposing dependency messages or response bodies.
            logger.LogWarning("Detached operation finished {RequestId} {FailureType}", requestId, error.GetType().Name);
        }
        finally { ReleaseLease(budget, capacity); completed(); }
    }

    public static WebApplication Build(ApiSettings settings, string? address, TokenCredential? credentialOverride = null)
    {
        if (settings.Mode is not ("fixture" or "azure") || settings.Concurrency is < 1 or > 32)
            throw new ArgumentException("Invalid API settings.");
        if (settings.RequestTimeoutSeconds is < 1 or > 180 || settings.MaxOutputTokens is < 128 or > 8192)
            throw new ArgumentException("Invalid request deadline or output token limit.");
        if (settings.WrongFixtureContract && settings.Mode != "fixture")
            throw new ArgumentException("Fault injection is restricted to local fixtures.");
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole();
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
        if (address is not null) builder.WebHost.UseUrls(address);
        builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 4096);
        builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(30));
        var capacity = new SemaphoreSlim(settings.Concurrency, settings.Concurrency);
        int drainingOperations = 0;
        AzureOpenAIClient? model = null;
        TokenCredential? credential = null;
        string deployment = "none";
        if (settings.Mode == "azure")
        {
            Uri endpoint = EndpointPolicy.Validate(EnvironmentFile.Required("AZURE_OPENAI_ENDPOINT"));
            deployment = EnvironmentFile.Required("AZURE_OPENAI_DEPLOYMENT_NAME");
            credential = credentialOverride ?? (settings.Identity switch
            {
                "cli" => new AzureCliCredential(),
                "managed" => new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned),
                "workload" => new WorkloadIdentityCredential(),
                _ => throw new ArgumentException("Use cli, managed or workload identity.")
            });
            model = new AzureOpenAIClient(endpoint, credential);
        }
        var app = builder.Build();
        app.Lifetime.ApplicationStopped.Register(capacity.Dispose);
        string instance = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")
            ?? Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName;
        object Health() => new { status = "ok", mode = settings.Mode, release = settings.Release,
            promptVersion = Lab.PromptVersion, instance,
            requestTimeoutSeconds = settings.RequestTimeoutSeconds, maxOutputTokens = settings.MaxOutputTokens,
            deadlinePolicy = "bounded-wait-v1", activeOperations = settings.Concurrency - capacity.CurrentCount,
            drainingOperations = Volatile.Read(ref drainingOperations) };
        app.MapGet("/health/live", () => Results.Json(Health()));
        app.MapGet("/health/ready", () => Results.Json(Health()));
        app.MapPost("/api/status", async Task<IResult> (StatusRequest request, HttpContext http) =>
        {
            string service = request.Service?.Trim().ToLowerInvariant() ?? "";
            if (!Catalog.Items.ContainsKey(service)) return Results.BadRequest(new { error = "unknown_service" });
            if (!await capacity.WaitAsync(0, http.RequestAborted))
            {
                http.Response.Headers["Retry-After"] = "1";
                return Results.Json(new { error = "busy" }, statusCode: 429);
            }
            string requestId = Guid.NewGuid().ToString("N");
            var watch = Stopwatch.StartNew();
            int outcome = 200;
            var progress = new RequestProgress(settings.Mode == "azure" ? "identity" : "fixture");
            Task<Evidence>? work = null;
            var budget = CancellationTokenSource.CreateLinkedTokenSource(
                http.RequestAborted, app.Lifetime.ApplicationStopping);
            budget.CancelAfter(TimeSpan.FromSeconds(settings.RequestTimeoutSeconds));
            try
            {
                // Dispatch behind the admission lease, including any synchronous SDK setup.
                work = Task.Run(() => ExecuteAsync(settings, service, model, credential, deployment, progress, budget.Token));
                // Bound the wait even when a dependency ignores its cancellation token.
                Evidence answer = await work.WaitAsync(budget.Token);
                budget.Token.ThrowIfCancellationRequested();
                return Results.Json(new { requestId, mode = settings.Mode, release = settings.Release,
                    promptVersion = Lab.PromptVersion, instance, answer.Service, answer.Status, answer.Summary });
            }
            catch (OperationCanceledException)
            {
                outcome = 504;
                string phase = progress.Phase;
                string reason = http.RequestAborted.IsCancellationRequested ? "client_disconnected"
                    : app.Lifetime.ApplicationStopping.IsCancellationRequested ? "application_stopping" : "request_deadline";
                app.Logger.LogWarning("Agent cancelled {RequestId} {Phase} {Reason}", requestId, phase, reason);
                return Results.Json(new { requestId, error = "request_cancelled", phase, reason,
                    timeoutSeconds = settings.RequestTimeoutSeconds, elapsedMs = watch.ElapsedMilliseconds }, statusCode: outcome);
            }
            catch (Exception error)
            {
                outcome = 502;
                string phase = progress.Phase;
                int? dependencyStatus = error switch
                {
                    System.ClientModel.ClientResultException clientError => clientError.Status,
                    Azure.RequestFailedException azureError => azureError.Status,
                    _ => null
                };
                app.Logger.LogWarning("Agent failure {RequestId} {Phase} {FailureType} {DependencyStatus}",
                    requestId, phase, error.GetType().Name, dependencyStatus);
                // Do not expose exception messages, credentials or model response bodies.
                return Results.Json(new { requestId, error = "agent_unavailable", phase,
                    failureType = error.GetType().Name, dependencyStatus, elapsedMs = watch.ElapsedMilliseconds }, statusCode: outcome);
            }
            finally
            {
                if (work is null || work.IsCompleted)
                {
                    // WaitAsync may stop waiting just as the operation faults.
                    _ = work?.Exception;
                    ReleaseLease(budget, capacity);
                }
                else
                {
                    // A timed-out HTTP request does not prove the underlying work stopped.
                    // Keep its slot until completion so retries cannot accumulate unbounded work.
                    Interlocked.Increment(ref drainingOperations);
                    _ = DrainAsync(work, budget, capacity, () => Interlocked.Decrement(ref drainingOperations), app.Logger, requestId);
                }
                app.Logger.LogInformation(
                    "Status completed {RequestId} {Service} {Outcome} {ElapsedMs} {Release} {Mode} {Instance}",
                    requestId, service, outcome, watch.ElapsedMilliseconds, settings.Release, settings.Mode, instance);
            }
        });
        return app;
    }
}
public sealed class ApiSession : IAsyncDisposable
{
    public WebApplication App { get; }
    public string Address { get; }
    private ApiSession(WebApplication app, string address) { App = app; Address = address; }
    public static async Task<ApiSession> StartAsync(ApiSettings settings, TokenCredential? credentialOverride = null)
    {
        var app = AgentHost.Build(settings, "http://127.0.0.1:0", credentialOverride);
        try
        {
            await app.StartAsync();
            string address = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single();
            return new(app, address);
        }
        catch { await app.DisposeAsync(); throw; }
    }
    public async ValueTask DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await App.StopAsync(timeout.Token);
        await App.DisposeAsync();
    }
}
