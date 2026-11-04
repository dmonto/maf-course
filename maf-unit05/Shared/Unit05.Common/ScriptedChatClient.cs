using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace Unit05.Common;

// A deterministic teaching double, not a model, evaluator or live-mode fallback.
// Function invocation, tool arguments, tool bodies and typed deserialization still use MAF.
public sealed class ScriptedChatClient(
    Func<SupportAnswer, IReadOnlyList<ToolResult>, string>? formatAnswer = null) : IChatClient
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var list = messages.ToList();
        string question = list.LastOrDefault(x => x.Role == ChatRole.User)?.Text ?? "";
        string[] services = Regex.Matches(question, "\\b(payroll|vpn|crm|billing)\\b", RegexOptions.IgnoreCase)
            .Select(m => m.Value.ToLowerInvariant()).Distinct().ToArray();
        var results = list.Where(m => m.Role == ChatRole.Tool).SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>().Select(ReadResult).ToArray();
        SupportAnswer answer;
        if (services.Length != 1)
            answer = new() { Outcome = "clarification", Summary = "Which single service do you mean?" };
        else
        {
            string service = services[0];
            bool action = Regex.IsMatch(question, "restart|create.*ticket|credentials|password", RegexOptions.IgnoreCase);
            bool read = Regex.IsMatch(question, "status|recovery|guidance|next step|what.*do", RegexOptions.IgnoreCase);
            if (action && !read)
                answer = new() { Outcome = "out-of-scope", Service = service,
                    Summary = "This read-only application cannot restart services, create tickets or disclose credentials." };
            else if (results.Length == 0)
            {
                bool guidance = Regex.IsMatch(question, "next step|guidance|what.*do", RegexOptions.IgnoreCase);
                bool status = !guidance || Regex.IsMatch(question, "status|recovery", RegexOptions.IgnoreCase);
                var calls = new List<AIContent>();
                void Add(string name)
                {
                    if (Equals(options?.ToolMode, ChatToolMode.None)) return;
                    if (options?.Tools?.OfType<AIFunction>().Any(t => t.Name == name) != true) return;
                    calls.Add(new FunctionCallContent(Guid.NewGuid().ToString("N"), name,
                        new Dictionary<string, object?> { ["serviceName"] = service }));
                }
                if (status) Add("GetServiceStatus");
                if (guidance) Add("GetSupportGuidance");
                if (calls.Count > 0)
                    return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, calls)));
                answer = new() { Outcome = "unavailable", Service = service,
                    Summary = "No required lookup evidence was returned." };
            }
            else if (results.Any(r => !r.Ok))
                answer = new() { Outcome = "unavailable", Service = service,
                    Summary = "No usable information is available to this caller (" + results.First(r => !r.Ok).Code + ")." };
            else
            {
                var rows = results.Select(r => r.Data!).ToArray();
                string? status = rows.FirstOrDefault(r => r.Status is not null)?.Status;
                string? step = rows.FirstOrDefault(r => r.NextStep is not null)?.NextStep;
                answer = new()
                {
                    Outcome = "answered", Service = service, Status = status, NextStep = step,
                    Sources = rows.Select(r => r.Source).Distinct().ToArray(), ActionExecuted = false,
                    Summary = string.Join(" ", new[]
                    {
                        status is null ? null : $"{service} is {status}. No recovery time is available.",
                        step is null ? null : $"Recommended next step: {step}. This recommendation performs no action."
                    }.Where(s => s is not null))
                };
            }
        }
        string json = formatAnswer?.Invoke(answer, results) ?? JsonSerializer.Serialize(answer);
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, json)));
    }

    private static ToolResult ReadResult(FunctionResultContent content)
    {
        string raw = content.Result switch
        {
            JsonElement e when e.ValueKind == JsonValueKind.String => e.GetString()!,
            JsonElement e => e.GetRawText(),
            string s => s,
            object o => JsonSerializer.Serialize(o),
            _ => "null"
        };
        return JsonSerializer.Deserialize<ToolResult>(raw, ReadOptions)
            ?? throw new InvalidOperationException("The scripted client requires a tool result.");
    }
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Unit 5 uses non-streaming structured responses.");
    public object? GetService(Type type, object? key = null) => key is null && type.IsInstanceOfType(this) ? this : null;
    public void Dispose() { }
}
