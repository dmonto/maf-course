using System.Diagnostics;
using Microsoft.Agents.AI;

internal sealed record ChatRequest(string? Message, string? ConversationId = null);

internal sealed class ConversationEntry(AgentSession session)
{
    public AgentSession Session { get; } = session;
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public int Turns { get; set; }
    public bool Invalidated { get; set; }
}

internal sealed class AgentChat(ILogger<AgentChat> logger)
{
    private readonly Dictionary<string, ConversationEntry> _sessions = [];
    private readonly object _sync = new();

    public async Task<IResult> RunAsync(
        ChatRequest request, AIAgent agent, HttpContext context)
    {
        string message = request.Message?.Trim() ?? "";
        string runId = (string)context.Items["RunId"]!;
        IResult Error(string code, int status) => Results.Json(
            new { code, runId }, statusCode: status);
        if (message.Length is 0 or > 2000)
            return Error("invalid-message", 400);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        string? id = request.ConversationId;
        ConversationEntry? entry;
        if (id is null)
        {
            var newEntry = new ConversationEntry(
                await agent.CreateSessionAsync(timeout.Token));
            lock (_sync)
            {
                if (_sessions.Count >= 20)
                    return Error("conversation-capacity", 429);
                id = Guid.NewGuid().ToString("N");
                _sessions.Add(id, newEntry);
            }
            entry = newEntry;
        }
        else
        {
            if (!Guid.TryParseExact(id, "N", out _))
                return Error("invalid-conversation-id", 400);
            lock (_sync) _sessions.TryGetValue(id, out entry);
            if (entry is null) return Error("unknown-conversation", 404);
        }

        // A session is mutable: refuse simultaneous turns for the same session.
        if (!await entry.Gate.WaitAsync(0, timeout.Token))
            return Error("conversation-busy", 409);
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ConversationId"] = id
        });
        var timer = Stopwatch.StartNew();
        try
        {
            if (entry.Invalidated) return Error("unknown-conversation", 404);
            if (entry.Turns >= 6) return Error("turn-limit", 409);
            logger.LogInformation("AgentStarted InputLength={InputLength}", message.Length);
            AgentResponse response = await agent.RunAsync(
                message, entry.Session, cancellationToken: timeout.Token);
            if (string.IsNullOrWhiteSpace(response.Text))
                throw new InvalidOperationException("No final text returned.");
            entry.Turns++;
            logger.LogInformation(
                "AgentCompleted Outcome={Outcome} ElapsedMs={ElapsedMs}",
                "completed", timer.ElapsedMilliseconds);
            return Results.Ok(new
            {
                runId, conversationId = id, outcome = "completed",
                turns = entry.Turns, answer = response.Text,
                elapsedMs = timer.ElapsedMilliseconds,
                evidence = (List<LookupResult>)context.Items["Evidence"]!
            });
        }
        catch (OperationCanceledException)
        {
            Invalidate(id!, entry);
            logger.LogWarning("AgentInterrupted Outcome={Outcome}", "cancelled");
            return Error("cancelled-start-new-conversation", 504);
        }
        catch (Exception ex)
        {
            Invalidate(id!, entry);
            logger.LogError("AgentFailed ErrorType={ErrorType}", ex.GetType().Name);
            return Error("agent-failed-start-new-conversation", 502);
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    private void Invalidate(string id, ConversationEntry entry)
    {
        entry.Invalidated = true;
        lock (_sync) _sessions.Remove(id);
    }
}
