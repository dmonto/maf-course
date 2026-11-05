using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

public static class MemoryPolicy
{
    public static TrimResult Trim(AgentSession session, int maxTurns = 4,
        int maxHistoryChars = 16_000)
    {
        if (maxTurns < 1 || maxHistoryChars < 1)
            throw new ArgumentOutOfRangeException(nameof(maxTurns));
        if (!session.TryGetInMemoryChatHistory(out var messages) || messages.Count == 0)
            return new(0, 0, 0);
        var turns = new List<List<ChatMessage>>();
        foreach (var message in messages)
        {
            if (message.Role == ChatRole.User) turns.Add([]);
            if (turns.Count == 0) throw new MemoryFault("invalid-history-boundary");
            turns[^1].Add(message);
        }
        int originalTurns = turns.Count;
        turns = turns.TakeLast(maxTurns).ToList();
        var kept = turns.SelectMany(t => t).ToList();
        int size = JsonSerializer.Serialize(kept).Length;
        while (size > maxHistoryChars && turns.Count > 0)
        {
            turns.RemoveAt(0);
            kept = turns.SelectMany(t => t).ToList();
            size = JsonSerializer.Serialize(kept).Length;
        }
        session.SetInMemoryChatHistory(kept);
        return new(originalTurns - turns.Count, kept.Count, size);
    }
}
