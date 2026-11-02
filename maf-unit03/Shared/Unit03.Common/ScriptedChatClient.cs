using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace Unit03.Common;

public enum SyntheticFault { None, BeforeTool, AfterTool, Timeout, EmptyResponse, RepeatedToolCalls }

// This is a deterministic teaching double, not an LLM or a fallback for a failed live request.
// MAF still performs function invocation and session management around this client.
public sealed class ScriptedChatClient(SyntheticFault fault = SyntheticFault.None) : IChatClient
{
    public int Requests { get; private set; }
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests++;
        if (fault == SyntheticFault.Timeout) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        if (fault == SyntheticFault.BeforeTool) throw new InvalidOperationException("Synthetic connection failure.");
        if (fault == SyntheticFault.EmptyResponse) return Text("");
        var list = messages.ToList();
        string question = list.LastOrDefault(x => x.Role == ChatRole.User)?.Text ?? "";
        bool toolReturned = list.LastOrDefault()?.Role == ChatRole.Tool;
        if (fault == SyntheticFault.AfterTool && toolReturned)
            throw new InvalidOperationException("Synthetic model failure after a successful lookup.");
        // Respect the final no-tools request made by the real function-invocation client.
        if (Equals(options?.ToolMode, ChatToolMode.None)
            || options?.Tools?.OfType<AIFunction>().Any() != true)
        {
            var latestToolResult = list.LastOrDefault(x => x.Role == ChatRole.Tool);
            return latestToolResult is not null ? Explain(latestToolResult)
                : Text("No additional tool calls are allowed; available evidence is insufficient.");
        }
        if (fault == SyntheticFault.RepeatedToolCalls)
            return Call("GetServiceStatus", "payroll");
        if (toolReturned) return Explain(list.Last());
        if (Regex.IsMatch(question, "restart|create.*ticket|close.*ticket", RegexOptions.IgnoreCase))
            return Text("This read-only application cannot restart services or create or close tickets.");
        string? service = ServiceIn(question);
        var priorServices = list.Where(x => x.Role == ChatRole.User).Select(x => ServiceIn(x.Text))
            .Where(x => x is not null).Distinct().ToArray();
        if (service is null)
        {
            if (priorServices.Length == 1) service = priorServices[0];
            else return Text("Which service do you mean: payroll, VPN or another service?");
        }
        string tool = Regex.IsMatch(question, "contact|who supports", RegexOptions.IgnoreCase)
            ? "GetSupportContact" : "GetServiceStatus";
        if (options?.Tools?.OfType<AIFunction>().Any(x => x.Name == tool) != true)
            return Text("This application has no registered capability for that request.");
        return Call(tool, service!);
    }

    static string? ServiceIn(string text) => Regex.Match(text,
        "\\b(payroll|vpn|crm|billing)\\b", RegexOptions.IgnoreCase) is { Success: true } match
        ? match.Value.ToLowerInvariant() : null;
    ChatResponse Call(string name, string service) => new(new ChatMessage(ChatRole.Assistant,
        [new FunctionCallContent("demo-call-" + Requests, name,
            new Dictionary<string, object?> { ["serviceName"] = service })]));
    static ChatResponse Text(string text) => new(new ChatMessage(ChatRole.Assistant, text));

    static ChatResponse Explain(ChatMessage message)
    {
        var content = message.Contents.OfType<FunctionResultContent>().LastOrDefault();
        if (content?.Result is null) return Text("The tool did not return usable evidence.");
        string raw = content.Result switch
        {
            JsonElement e when e.ValueKind == JsonValueKind.String => e.GetString()!,
            JsonElement e => e.GetRawText(),
            string s => s,
            object value => JsonSerializer.Serialize(value, Audit.JsonOptions)
        };
        using var parsed = JsonDocument.Parse(raw);
        var root = parsed.RootElement;
        if (!root.GetProperty("ok").GetBoolean())
            return Text("No usable information is available to this caller (" + root.GetProperty("code").GetString() + ").");
        var data = root.GetProperty("data");
        if (data.TryGetProperty("email", out var email))
            return Text($"Contact {email.GetString()} for {data.GetProperty("service").GetString()}. Source: {data.GetProperty("source").GetString()} (synthetic).");
        return Text($"{data.GetProperty("service").GetString()}: {data.GetProperty("status").GetString()}. {data.GetProperty("detail").GetString()} Source: {data.GetProperty("source").GetString()} (synthetic).");
    }
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This teaching double implements non-streaming requests only.");
    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    public void Dispose() { }
}
