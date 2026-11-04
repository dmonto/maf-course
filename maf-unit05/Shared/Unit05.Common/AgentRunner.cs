using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace Unit05.Common;

public sealed record RunReport<T>(
    string RunId, string ExecutionMode, string PromptVersion, string PromptSha256,
    string Deployment, bool Injection, string Outcome, long ElapsedMs,
    string[] Errors, IReadOnlyList<ToolEvent> Evidence, T? Answer,
    int MaxOutputTokens, int MaxIterations, int TimeoutSeconds,
    float? Temperature = null, float? TopP = null) where T : SupportAnswer;

public static class AgentRunner
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static ActorContext Caller() => new("student-001",
        new HashSet<string>(["payroll", "vpn", "billing"], StringComparer.Ordinal));

    public static Task<RunReport<SupportAnswer>> RunAsync(CourseSettings settings, string message,
        bool injection = false) => RunAsync<SupportAnswer>(settings, message, injection, AnswerValidator.Check);

    public static async Task<RunReport<T>> RunAsync<T>(CourseSettings settings, string message,
        bool injection, Func<T, IReadOnlyList<ToolEvent>, string[]> validate,
        string extraInstructions = "",
        Func<SupportAnswer, IReadOnlyList<ToolResult>, string>? demoFormatter = null) where T : SupportAnswer
    {
        string runId = Guid.NewGuid().ToString("N");
        string deployment = settings.Demo ? "none-scripted-model" : CourseSettings.Setting("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "";
        string hash = "";
        var timer = Stopwatch.StartNew();
        var tools = new ServiceTools(Caller(), injection);
        RunReport<T> Report(string outcome, string[] errors, T? answer) => new(
            runId, settings.Demo ? "demo-scripted-model" : "live-azure", settings.PromptVersion,
            hash, deployment, injection, outcome, timer.ElapsedMilliseconds,
            errors, tools.Evidence.ToArray(), answer,
            settings.MaxOutputTokens, settings.MaxIterations, settings.TimeoutSeconds);
        try
        {
            message = message.Trim();
            if (message.Length is 0 or > 2000) throw new ConfigurationException("Supply 1 to 2000 characters.");
            string instructions = File.ReadAllText(Path.Combine(settings.Root, "Prompts", $"service-desk.{settings.PromptVersion}.txt"));
            if (string.IsNullOrWhiteSpace(instructions)) throw new ConfigurationException("The prompt file is empty.");
            instructions += extraInstructions.Length == 0 ? "" : "\n" + extraInstructions;
            hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(instructions))).ToLowerInvariant();
            IChatClient raw;
            if (settings.Demo) raw = new ScriptedChatClient(demoFormatter);
            else
            {
                string endpoint = CourseSettings.Required("AZURE_OPENAI_ENDPOINT");
                deployment = CourseSettings.Required("AZURE_OPENAI_DEPLOYMENT_NAME");
                if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                    uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                    throw new ConfigurationException("Use the assigned HTTPS resource-root endpoint.");
                raw = new AzureOpenAIClient(uri, new AzureCliCredential()).GetChatClient(deployment).AsIChatClient();
            }
            using var bounded = new FunctionInvokingChatClient(raw)
            {
                MaximumIterationsPerRequest = settings.MaxIterations,
                AllowConcurrentInvocation = false,
                IncludeDetailedErrors = false
            };
            AIAgent agent = bounded.AsAIAgent(new ChatClientAgentOptions
            {
                Name = "service-desk-design", UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = instructions, MaxOutputTokens = settings.MaxOutputTokens,
                    ToolMode = ChatToolMode.Auto,
                    Tools = [AIFunctionFactory.Create(tools.GetServiceStatus), AIFunctionFactory.Create(tools.GetSupportGuidance)]
                }
            });
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            var response = await agent.RunAsync<T>(message, cancellationToken: timeout.Token);
            T answer = response.Result;
            string[] errors = validate(answer, tools.Evidence);
            return Report(errors.Length == 0 ? "contract-accepted" : "contract-rejected", errors,
                errors.Length == 0 ? answer : null);
        }
        catch (Exception ex)
        {
            return Report("execution-failed", [ex.GetType().Name], null);
        }
    }
    public static int ExitCode<T>(RunReport<T> report) where T : SupportAnswer
        => report.Outcome == "contract-accepted" ? 0 : report.Outcome == "contract-rejected" ? 2 : 1;
}
