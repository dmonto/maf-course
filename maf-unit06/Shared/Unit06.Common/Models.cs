using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Unit06.Common;

public sealed record ActorContext(string TenantId, string UserId);
// The exercise may extend this record without changing the stable repository.
public record CaseState(string CaseId, string Service, string Stage);
public sealed record StoredConversation(long Revision, string AgentConfig,
    JsonElement BusinessState, JsonElement Session, long ExpiresUnix)
{
    public T ReadBusinessState<T>() where T : CaseState =>
        BusinessState.Deserialize<T>() ?? throw new MemoryFault("invalid-state");
}
public sealed record TrimResult(int DroppedTurns, int Messages, int HistoryChars);
public sealed record TurnAudit(string RunId, long Revision, string CommittedUtc,
    int InputChars, int OutputChars, int DroppedTurns, long ElapsedMs);
public sealed class MemoryFault(string code) : Exception(code)
{
    public string Code { get; } = code;
}
public sealed class ConfigurationException(string message) : Exception(message);

public sealed record TurnPolicy<TState>(string Version, Func<TState> CreateState)
    where TState : CaseState
{
    public TimeSpan IdleLifetime { get; init; } = TimeSpan.FromHours(24);
    public Func<TState, TState>? PrepareState { get; init; }
    public Func<ActorContext, TState, ToolTrace, List<AITool>>? CreateTools { get; init; }
    public string AdditionalInstructions { get; init; } = "";
}
public sealed record TurnResult(string Outcome, string Execution, string SessionId,
    string Replica, string RunId, long Revision, bool Restored, int RetainedMessages,
    int HistoryChars, int DroppedTurns, long ModelElapsedMs, string[] ToolCalls,
    string Answer);

public sealed class ToolTrace
{
    private readonly List<string> _calls = [];
    public void Add(string name) => _calls.Add(name);
    public string[] Calls => _calls.ToArray();
}

public static class Lab
{
    // These host-owned values are synthetic. A web host must use verified claims.
    public static ActorContext Actor { get; } = new("lab-tenant", "student-001");
    public static CaseState NewCase() => new("INC-DEMO-001", "vpn", "investigating");
    public static bool CanReadCase(ActorContext actor, CaseState state) =>
        actor == Actor && state.CaseId == "INC-DEMO-001" && state.Service == "vpn";
    public static TurnPolicy<CaseState> DefaultPolicy { get; } = new("baseline-v1", NewCase);
}

public static class JsonOutput
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
    public static void Write(object value) =>
        Console.WriteLine(JsonSerializer.Serialize(value, Options));
}
