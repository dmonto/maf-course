using System.Diagnostics;
using System.Net;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Unit04.Common;

public static class LabHost
{
    public static async Task<int> RunAsync(string[] args, LabProfile profile, string example,
        Action<IServiceCollection>? configureServices = null)
    {
        try
        {
            await using var app = Build(args, profile, example, configureServices);
            await app.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                status = "error", errorType = ex.GetType().Name,
                message = ex is ConfigurationException ? ex.Message : "Check host configuration and its preceding diagnostic log."
            }));
            return 1;
        }
    }

    public static WebApplication Build(string[] args, LabProfile profile, string example,
        Action<IServiceCollection>? configureServices = null)
    {
        var settings = CourseSettings.Load(args);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [], ContentRootPath = settings.Root,
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseUrls($"http://127.0.0.1:{settings.Port}");
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        });
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ServiceTools>();
        builder.Services.AddSingleton<IChatHandler, AgentChat>();
        builder.Services.AddHostedService<SpanLogListener>();
        if (profile != LabProfile.ConfigurationOnly)
        {
            string path = Path.Combine(settings.Root, "Prompts", $"service-desk.{settings.PromptVersion}.txt");
            string instructions = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(instructions)) throw new ConfigurationException("The instruction file is empty.");
            if (profile == LabProfile.Registration)
                instructions = "You are a registered English-language service-desk assistant. " +
                    "No business tool is registered in this profile. Explain that current status needs the next example; never invent a status.";
            if (settings.Demo)
                builder.Services.AddSingleton<IChatClient, ScriptedChatClient>();
            else
            {
                string endpoint = CourseSettings.Required("AZURE_OPENAI_ENDPOINT");
                string deployment = CourseSettings.Required("AZURE_OPENAI_DEPLOYMENT_NAME");
                if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                    uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                    throw new ConfigurationException("Use the assigned HTTPS resource-root endpoint, without /openai/v1/ or a project path.");
                builder.Services.AddSingleton<IChatClient>(_ =>
                    new AzureOpenAIClient(uri, new AzureCliCredential()).GetChatClient(deployment).AsIChatClient());
            }
            builder.AddAIAgent("service-desk", (sp, name) =>
            {
                var bounded = new FunctionInvokingChatClient(sp.GetRequiredService<IChatClient>())
                {
                    MaximumIterationsPerRequest = settings.MaxIterations,
                    AllowConcurrentInvocation = false,
                    IncludeDetailedErrors = false
                };
                var options = new ChatClientAgentOptions
                {
                    Name = name, Description = "Read-only synthetic service-status assistant.",
                    UseProvidedChatClientAsIs = true,
                    ChatOptions = new ChatOptions
                    {
                        Instructions = instructions, MaxOutputTokens = settings.MaxOutputTokens,
                        Tools = profile == LabProfile.Registration ? [] :
                            [AIFunctionFactory.Create(sp.GetRequiredService<ServiceTools>().GetServiceStatus)]
                    }
                };
                return bounded.AsAIAgent(options).AsBuilder().UseOpenTelemetry(
                    sourceName: "Unit04.ServiceDesk",
                    configure: options => options.EnableSensitiveData = false).Build();
            }).WithInMemorySessionStore(withIsolation: false);
            if (profile == LabProfile.Full)
            {
                builder.AddDevUI();
                builder.Services.AddOpenAIResponses();
                builder.Services.AddOpenAIConversations();
            }
        }
        // Exercise 2 replaces only IChatHandler; ordinary projects retain the baseline handler.
        configureServices?.Invoke(builder.Services);
        var app = builder.Build();
        if (profile != LabProfile.ConfigurationOnly)
            _ = app.Services.GetRequiredKeyedService<AIAgent>("service-desk");
        // Resolve a custom handler at startup so invalid expiry settings fail before serving.
        _ = app.Services.GetRequiredService<IChatHandler>();
        app.Use(async (context, next) =>
        {
            if (!IPAddress.IsLoopback(context.Connection.RemoteIpAddress ?? IPAddress.None))
            {
                context.Response.StatusCode = 403;
                return;
            }
            string runId = Guid.NewGuid().ToString("N");
            context.Items["RunId"] = runId;
            context.Items["Evidence"] = new List<LookupResult>();
            context.Response.Headers["X-Run-Id"] = runId;
            using var scope = app.Logger.BeginScope(new Dictionary<string, object?>
            {
                ["RunId"] = runId, ["Agent"] = "service-desk",
                ["PromptVersion"] = settings.PromptVersion,
                ["ExecutionMode"] = settings.Demo ? "demo" : "live", ["Example"] = example
            });
            var timer = Stopwatch.StartNew();
            try { await next(context); }
            finally
            {
                app.Logger.LogInformation(
                    "RequestCompleted HttpStatus={HttpStatus} ElapsedMs={ElapsedMs} ToolCalls={ToolCalls}",
                    context.Response.StatusCode, timer.ElapsedMilliseconds,
                    ((List<LookupResult>)context.Items["Evidence"]!).Count);
            }
        });
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok", agent = "service-desk", example,
            agentRegistered = profile != LabProfile.ConfigurationOnly,
            toolRegistered = profile is LabProfile.Tools or LabProfile.Full,
            devUI = profile == LabProfile.Full,
            promptVersion = settings.PromptVersion,
            executionMode = settings.Demo ? "demo-scripted-model" : "live-azure",
            deployment = settings.Demo ? "none-scripted-model" : CourseSettings.Setting("AZURE_OPENAI_DEPLOYMENT_NAME"),
            note = "Host readiness only; inference is checked by a chat request."
        }));
        if (profile != LabProfile.ConfigurationOnly)
            app.MapPost("/api/chat", (ChatRequest request,
                [FromKeyedServices("service-desk")] AIAgent agent,
                IChatHandler chat, HttpContext context) => chat.RunAsync(request, agent, context));
        if (profile == LabProfile.Full)
        {
            app.MapOpenAIResponses();
            app.MapOpenAIConversations();
            app.MapDevUI();
        }
        app.Logger.LogInformation(
            "HostReady Agent={Agent} Deployment={Deployment} PromptVersion={PromptVersion} Mode={Mode} Example={Example}",
            "service-desk", settings.Demo ? "none-scripted-model" : CourseSettings.Setting("AZURE_OPENAI_DEPLOYMENT_NAME"),
            settings.PromptVersion, settings.Demo ? "demo" : "live", example);
        return app;
    }
}

internal sealed class SpanLogListener(ILogger<SpanLogListener> logger) : IHostedService, IDisposable
{
    private ActivityListener? _listener;
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Unit04.ServiceDesk",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => logger.LogInformation(
                "SpanCompleted Span={Span} TraceId={TraceId} DurationMs={DurationMs} Status={Status}",
                activity.DisplayName, activity.TraceId.ToString(), activity.Duration.TotalMilliseconds,
                activity.Status.ToString())
        };
        ActivitySource.AddActivityListener(_listener);
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken) { Dispose(); return Task.CompletedTask; }
    public void Dispose() { _listener?.Dispose(); _listener = null; }
}
