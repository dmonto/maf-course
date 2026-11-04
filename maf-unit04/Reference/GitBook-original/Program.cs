using System.Diagnostics;
using System.Net;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Hosting.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Run this local laboratory in Development.");
builder.WebHost.UseUrls("http://127.0.0.1:5050");
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

string endpoint = Required(builder.Configuration, "AZURE_OPENAI_ENDPOINT");
string deployment = Required(builder.Configuration, "AZURE_OPENAI_DEPLOYMENT_NAME");
if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) ||
    endpointUri.Scheme != "https" || endpointUri.AbsolutePath != "/" ||
    endpointUri.Query.Length != 0)
    throw new InvalidOperationException("Use the assigned HTTPS resource-root endpoint.");
string version = Required(builder.Configuration, "Agent:PromptVersion");
if (version is not ("v1" or "v2"))
    throw new InvalidOperationException("PromptVersion must be v1 or v2.");
string instructions = File.ReadAllText(Path.Combine(
    builder.Environment.ContentRootPath, "Prompts", $"service-desk.{version}.txt"));
if (string.IsNullOrWhiteSpace(instructions))
    throw new InvalidOperationException("The instruction file is empty.");

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(new AzureOpenAIClient(endpointUri, new AzureCliCredential()));
builder.Services.AddSingleton<ServiceTools>();
builder.Services.AddSingleton<AgentChat>();
builder.AddAIAgent("service-desk", (sp, name) =>
{
    var client = sp.GetRequiredService<AzureOpenAIClient>();
    var tools = sp.GetRequiredService<ServiceTools>();
    var agent = client.GetChatClient(deployment).AsAIAgent(
        new ChatClientAgentOptions
        {
            Name = name,
            Description = "Read-only synthetic service-status assistant.",
            UseProvidedChatClientAsIs = true,
            ChatOptions = new ChatOptions
            {
                Instructions = instructions,
                MaxOutputTokens = 800,
                Tools = [AIFunctionFactory.Create(tools.GetServiceStatus)]
            }
        },
        clientFactory: inner => new FunctionInvokingChatClient(inner)
        {
            MaximumIterationsPerRequest = 4,
            AllowConcurrentInvocation = false
        });
    return agent.AsBuilder().UseOpenTelemetry(
        sourceName: "Unit04.ServiceDesk",
        configure: options => options.EnableSensitiveData = false).Build();
}).WithInMemorySessionStore(withIsolation: false);
builder.AddDevUI();
builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();

var app = builder.Build();
// Resolve registration at startup; this does not call Azure inference.
_ = app.Services.GetRequiredKeyedService<AIAgent>("service-desk");
var spanLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Lab.Spans");
using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == "Unit04.ServiceDesk",
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
    ActivityStopped = activity => spanLogger.LogInformation(
        "SpanCompleted Span={Span} TraceId={TraceId} DurationMs={DurationMs} Status={Status}",
        activity.DisplayName, activity.TraceId.ToString(), activity.Duration.TotalMilliseconds,
        activity.Status.ToString())
};
ActivitySource.AddActivityListener(listener);

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
        ["RunId"] = runId, ["Agent"] = "service-desk", ["PromptVersion"] = version
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
    status = "ok", agent = "service-desk", promptVersion = version,
    note = "Host readiness only; inference is checked by a chat request."
}));
app.MapPost("/api/chat", (ChatRequest request,
    [FromKeyedServices("service-desk")] AIAgent agent,
    AgentChat chat, HttpContext context) => chat.RunAsync(request, agent, context));
app.MapOpenAIResponses();
app.MapOpenAIConversations();
app.MapDevUI();
app.Logger.LogInformation(
    "HostReady Agent={Agent} Deployment={Deployment} PromptVersion={PromptVersion}",
    "service-desk", deployment, version);
await app.RunAsync();

static string Required(IConfiguration configuration, string name)
{
    string value = configuration[name]?.Trim() ?? "";
    if (value.Length == 0 || value.Contains('<') || value.Contains('>'))
        throw new InvalidOperationException($"Set {name} to its assigned value.");
    return value;
}
