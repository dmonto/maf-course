using System.Text.Json;

public sealed record ActorContext(string TenantId, string UserId);
public sealed record CaseState(string CaseId, string Service, string Stage);
public sealed record StoredConversation(long Revision, string AgentConfig,
    CaseState BusinessState, JsonElement Session, long ExpiresUnix);
public sealed record TrimResult(int DroppedTurns, int Messages, int HistoryChars);
public sealed record TurnAudit(string RunId, long Revision, string CommittedUtc,
    int InputChars, int OutputChars, int DroppedTurns, long ElapsedMs);
public sealed class MemoryFault(string code) : Exception(code)
{
    public string Code { get; } = code;
}
