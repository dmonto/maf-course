using System.Diagnostics;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

try
{
    string version = Environment.GetEnvironmentVariable("PROMPT_VERSION") ?? "v2";
    if (version is not ("v1" or "v2")) throw new InvalidOperationException("Invalid prompt version.");
    bool injection = args.Contains("--injection");
    string message = string.Join(" ", args.Where(a => a != "--injection")).Trim();
    if (message.Length is 0 or > 2000) throw new ArgumentException("Supply 1 to 2000 characters.");
    string endpoint = Required("AZURE_OPENAI_ENDPOINT");
    if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
        uri.Scheme != "https" || uri.AbsolutePath != "/" || uri.Query.Length != 0)
        throw new InvalidOperationException("Use the assigned HTTPS resource-root endpoint.");
    string deployment = Required("AZURE_OPENAI_DEPLOYMENT_NAME");
    string instructions = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Prompts", $"service-desk.{version}.txt"));
    if (string.IsNullOrWhiteSpace(instructions)) throw new InvalidOperationException("Empty prompt.");
    var actor = new ActorContext("student-001",
        new HashSet<string>(["payroll", "vpn", "billing"], StringComparer.Ordinal));
    var tools = new ServiceTools(actor, injection);
    var client = new AzureOpenAIClient(uri, new AzureCliCredential());
    AIAgent agent = client.GetChatClient(deployment).AsAIAgent(
        new ChatClientAgentOptions
        {
            Name = "service-desk-design",
            UseProvidedChatClientAsIs = true,
            ChatOptions = new ChatOptions
            {
                Instructions = instructions,
                MaxOutputTokens = 1600,
                ToolMode = ChatToolMode.Auto,
                Tools = [AIFunctionFactory.Create(tools.GetServiceStatus),
                    AIFunctionFactory.Create(tools.GetSupportGuidance)]
            }
        },
        clientFactory: inner => new FunctionInvokingChatClient(inner)
        {
            MaximumIterationsPerRequest = 4,
            AllowConcurrentInvocation = false,
            IncludeDetailedErrors = false
        });
    string runId = Guid.NewGuid().ToString("N");
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
    var timer = Stopwatch.StartNew();
    var response = await agent.RunAsync<SupportAnswer>(message, cancellationToken: timeout.Token);
    var answer = response.Result;
    string[] errors = AnswerValidator.Check(answer, tools.Evidence);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        runId, promptVersion = version, deployment, injection,
        outcome = errors.Length == 0 ? "contract-accepted" : "contract-rejected",
        elapsedMs = timer.ElapsedMilliseconds, errors, evidence = tools.Evidence,
        answer = errors.Length == 0 ? answer : null
    }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    if (errors.Length != 0) Environment.ExitCode = 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(new
    {
        outcome = "execution-failed", errorType = ex.GetType().Name
    }));
    Environment.ExitCode = 1;
}

static string Required(string name)
{
    string value = Environment.GetEnvironmentVariable(name)?.Trim() ?? "";
    return value.Length == 0 || value.Contains('<')
        ? throw new InvalidOperationException($"Load the assigned {name}.") : value;
}
