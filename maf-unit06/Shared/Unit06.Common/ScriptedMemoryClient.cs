using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace Unit06.Common;

// An explicitly selected deterministic client. It is not an LLM or a live fallback.
// Function calls still pass through MAF's real registered-tool invocation pipeline.
public sealed class ScriptedMemoryClient : IChatClient
{
    private static long _totalCalls;
    public static long TotalCalls => Interlocked.Read(ref _totalCalls);

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _totalCalls);
        var list = messages.ToList();
        int index = list.FindLastIndex(m => m.Role == ChatRole.User);
        string input = index < 0 ? "" : list[index].Text;
        string lower = input.ToLowerInvariant();
        var results = list.Skip(index + 1).SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>().Select(c => c.Result).ToList();
        if (results.Count > 0)
            return Answer("Synthetic tool evidence: " + JsonSerializer.Serialize(results));

        var calls = new List<AIContent>();
        var available = options?.Tools?.OfType<AIFunction>()
            .Select(t => t.Name).ToHashSet(StringComparer.Ordinal) ?? [];
        if ((lower.Contains("case") || lower.Contains("stage") || lower.Contains("recorded operating"))
            && available.Contains("GetCaseContext"))
            calls.Add(new FunctionCallContent(Guid.NewGuid().ToString("N"),
                "GetCaseContext", new Dictionary<string, object?>()));
        if ((lower.Contains("status") || lower.Contains("currently healthy")) &&
            available.Contains("GetServiceStatus"))
        {
            string? service = lower.Contains("payroll") ? "payroll" :
                lower.Contains("vpn") ? "vpn" : null;
            if (service is not null)
                calls.Add(new FunctionCallContent(Guid.NewGuid().ToString("N"),
                    "GetServiceStatus", new Dictionary<string, object?> { ["service"] = service }));
        }
        if (calls.Count > 0)
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, calls)));

        if (lower.StartsWith("what ") || lower.StartsWith("which "))
        {
            // Only retained user messages are inspected. No global memory is used.
            string history = string.Join("\n", list.Take(Math.Max(index, 0))
                .Where(m => m.Role == ChatRole.User).Select(m => m.Text));
            var facts = new List<string>();
            if (history.Contains("Windows 11", StringComparison.OrdinalIgnoreCase)) facts.Add("Windows 11");
            if (Regex.IsMatch(history, "\\berror\\s+809\\b", RegexOptions.IgnoreCase)) facts.Add("error 809");
            var marker = Regex.Matches(history, "marker is ([a-z0-9-]+)", RegexOptions.IgnoreCase);
            if (marker.Count > 0) facts.Add("marker " + marker[^1].Groups[1].Value);
            return Answer(facts.Count > 0 ? "You reported: " + string.Join(", ", facts) + "." :
                "That detail is absent from this session's selected context. Please provide it.");
        }
        return Answer("Recorded this user-reported turn. No business action was executed.");
    }

    private static Task<ChatResponse> Answer(string text) =>
        Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This unit demonstrates non-streaming runs.");
    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
