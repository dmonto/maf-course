using System.Diagnostics;
using Microsoft.Agents.AI;

namespace Unit06.Common;

public sealed class TurnEngine
{
    private readonly CourseSettings _settings;
    private readonly TimeProvider _clock;
    public ConversationStore Store { get; }
    public TurnEngine(CourseSettings settings, TimeProvider? clock = null)
    {
        _settings = settings;
        _clock = clock ?? TimeProvider.System;
        Store = new(settings.DatabasePath, _clock);
    }

    public async Task<TurnResult> ExecuteAsync<TState>(string sessionId, string message,
        TurnPolicy<TState> policy, string replica = "console") where TState : CaseState
    {
        if (message.Trim().Length is 0 or > 1000) throw new MemoryFault("invalid-input");
        var stored = Store.Load(Lab.Actor, sessionId);
        string config = AgentFactory.ConfigurationKey(_settings, policy.Version);
        if (stored is not null && stored.AgentConfig != config)
            throw new MemoryFault("config-mismatch");
        TState state = stored is null ? policy.CreateState() : stored.ReadBusinessState<TState>();
        if (policy.PrepareState is not null) state = policy.PrepareState(state);
        var trace = new ToolTrace();
        var tools = policy.CreateTools is null ? MemoryTools.Create(Lab.Actor, state, trace) :
            policy.CreateTools(Lab.Actor, state, trace);
        AIAgent agent = AgentFactory.Create(_settings, tools, policy.AdditionalInstructions);
        AgentSession session = stored is null ? await agent.CreateSessionAsync() :
            await agent.DeserializeSessionAsync(stored.Session);
        var before = MemoryPolicy.Trim(session);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_settings.TimeoutSeconds));
        var timer = Stopwatch.StartNew();
        var response = await agent.RunAsync(message, session, cancellationToken: timeout.Token);
        var after = MemoryPolicy.Trim(session);
        var serialized = await agent.SerializeSessionAsync(session);
        long revision = (stored?.Revision ?? 0) + 1;
        string runId = Guid.NewGuid().ToString("N");
        var audit = new TurnAudit(runId, revision, _clock.GetUtcNow().ToString("O"),
            message.Length, response.Text.Length, before.DroppedTurns + after.DroppedTurns,
            timer.ElapsedMilliseconds);
        Store.Save(Lab.Actor, sessionId, revision - 1, config, state, serialized, audit,
            policy.IdleLifetime);
        // Return the answer only after the candidate state and metadata have committed.
        return new("committed", _settings.Execution, sessionId, replica, runId, revision,
            stored is not null, after.Messages, after.HistoryChars, audit.DroppedTurns,
            audit.ElapsedMs, trace.Calls, response.Text);
    }
}
