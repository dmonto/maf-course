using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
namespace Unit04.Common;
using System.Diagnostics;
using Microsoft.Agents.AI;

public sealed record ChatRequest(string? Message, string? ConversationId = null);

public sealed class ConversationEntry(AgentSession session)
{
    public AgentSession Session { get; } = session;
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public int Turns { get; set; }
    public bool Invalidated { get; set; }
}

public sealed class AgentChat(ILogger<AgentChat> logger, CourseSettings settings) : IChatHandler
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
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        string? id = request.ConversationId;
        ConversationEntry? entry;
        if (id is null)
        {
            var newEntry = new ConversationEntry(
                await agent.CreateSessionAsync(timeout.Token));
            lock (_sync)
            {
                if (_sessions.Count >= settings.MaxConversations)
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
            if (entry.Turns >= settings.MaxTurns) return Error("turn-limit", 409);
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
        catch (System.ClientModel.ClientResultException ex)
            when (ex.Status == 400 && ex.Message.Contains("content_filter", StringComparison.OrdinalIgnoreCase))
        {
            // Azure's prompt filter (e.g. jailbreak detection) rejected the input before the model ran.
            // Report a controlled refusal; the session may hold the rejected turn, so retire it.
            Invalidate(id!, entry);
            logger.LogWarning("AgentBlocked Outcome={Outcome} UpstreamStatus={UpstreamStatus}", "blocked-by-policy", ex.Status);
            return Results.Ok(new
            {
                runId, conversationId = id, outcome = "blocked-by-policy",
                turns = entry.Turns, answer = "This request was blocked by the service's content-safety policy. No action was taken and no status was changed.",
                elapsedMs = timer.ElapsedMilliseconds,
                evidence = (List<LookupResult>)context.Items["Evidence"]!
            });
        }
        catch (Exception ex)
        {
            Invalidate(id!, entry);
            logger.LogError("AgentFailed ErrorType={ErrorType} UpstreamStatus={UpstreamStatus} Detail={Detail}",
                ex.GetType().Name,
                (ex as System.ClientModel.ClientResultException)?.Status,
                ex is System.ClientModel.ClientResultException ? ex.Message : null);
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
