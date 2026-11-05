using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Unit06.Common;

public static class Examples
{
    public static async Task<object> InSessionAsync(CourseSettings settings, Arguments args)
    {
        var trace = new ToolTrace();
        AIAgent agent = AgentFactory.Create(settings, MemoryTools.Create(Lab.Actor, Lab.NewCase(), trace));
        AgentSession session = await agent.CreateSessionAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        var first = await agent.RunAsync(args.Input(
            "My test marker is copper-51. I use Windows 11 and VPN error 809."),
            session, cancellationToken: timeout.Token);
        const string followUp = "What test marker, operating system and VPN error did I report?";
        var second = await agent.RunAsync(followUp, session, cancellationToken: timeout.Token);
        var freshSession = await agent.CreateSessionAsync();
        var fresh = await agent.RunAsync("What test marker did I report?", freshSession,
            cancellationToken: timeout.Token);
        return new
        {
            example = "v01", execution = settings.Execution, persistence = "in-memory-only",
            firstAnswer = first.Text, sameSessionAnswer = second.Text,
            freshSessionAnswer = fresh.Text, sameSessionMessages = MemoryPolicy.Trim(session).Messages,
            freshSessionMessages = MemoryPolicy.Trim(freshSession).Messages
        };
    }

    public static async Task<object> PersistenceAsync(CourseSettings settings, Arguments args)
    {
        var engine = new TurnEngine(settings);
        string id = args.Session("v02-persistent-" + settings.ModeSuffix);
        if (args.Has("--audit")) return new { example = "v02", sessionId = id,
            execution = "local-no-inference", audit = engine.Store.ReadAudit(Lab.Actor, id) };
        return await engine.ExecuteAsync(id, args.Input(
            "My test marker is copper-51. I use Windows 11 and VPN error 809."),
            Lab.DefaultPolicy, args.Get("--replica") ?? "console");
    }

    public static async Task<object> TruncationAsync(CourseSettings settings, Arguments args)
    {
        // Creating a session does not call the inference service. This probe is always local.
        var local = settings with { Demo = true };
        var agent = AgentFactory.Create(local, []);
        var session = await agent.CreateSessionAsync();
        var messages = new List<ChatMessage>();
        for (int i = 1; i <= 6; i++)
        {
            messages.Add(new(ChatRole.User, "User turn " + i));
            messages.Add(new(ChatRole.Assistant, [new FunctionCallContent("call-" + i,
                "GetCaseContext", new Dictionary<string, object?>())]));
            messages.Add(new(ChatRole.Tool, [new FunctionResultContent("call-" + i,
                new { code = "found", caseId = "INC-DEMO-001" })]));
            messages.Add(new(ChatRole.Assistant, "Final answer " + i));
        }
        session.SetInMemoryChatHistory(messages);
        var reduced = MemoryPolicy.Trim(session);
        if (!session.TryGetInMemoryChatHistory(out var retained))
            throw new MemoryFault("missing-history");
        var calls = retained.SelectMany(m => m.Contents).OfType<FunctionCallContent>()
            .Select(c => c.CallId).ToArray();
        var results = retained.SelectMany(m => m.Contents).OfType<FunctionResultContent>()
            .Select(c => c.CallId).ToArray();
        if (reduced.DroppedTurns != 2 || retained.Count != 16 || !calls.SequenceEqual(results))
            throw new MemoryFault("invalid-truncation-result");
        var oversized = MemoryPolicy.Trim(session, maxTurns: 4, maxHistoryChars: 5);
        return new
        {
            example = "v03", execution = "local-no-inference", originalTurns = 6,
            keptTurns = 4, reduced.DroppedTurns, retainedMessages = reduced.Messages,
            reduced.HistoryChars, functionCallIds = calls, functionResultIds = results,
            completeToolPairs = calls.SequenceEqual(results), oversizedTurnResult = oversized
        };
    }

    public static async Task<object> EnrichmentAsync(CourseSettings settings, Arguments args)
    {
        var engine = new TurnEngine(settings);
        string id = args.Session("v04-context-" + settings.ModeSuffix);
        if (args.Has("--audit")) return new { example = "v04", sessionId = id,
            execution = "local-no-inference", audit = engine.Store.ReadAudit(Lab.Actor, id) };
        return await engine.ExecuteAsync(id, args.Input("What is my case reference and stage?"),
            Lab.DefaultPolicy, args.Get("--replica") ?? "console");
    }

    public static async Task<object> ReplicaAsync(CourseSettings settings, Arguments args)
    {
        string id = args.Session("v05-replica-" + settings.ModeSuffix);
        string replica = args.Get("--replica") ?? "A";
        var engine = new TurnEngine(settings);
        string message = args.Input(replica.Equals("A", StringComparison.OrdinalIgnoreCase)
            ? "My test marker is copper-51. I use Windows 11 and VPN error 809."
            : "What test marker, operating system and VPN error did I report?");
        var turn = await engine.ExecuteAsync(id, message, Lab.DefaultPolicy, replica);
        return new
        {
            example = "v05", topology = "single-machine-replica-simulation",
            storage = "one shared local SQLite database", turn,
            explanation = "Run replica A and replica B as separate processes. " +
                "The common database is the recovery source. This is not a networked SQLite deployment."
        };
    }

    public static async Task<object> ConsistencyAsync(CourseSettings settings, Arguments args)
    {
        string id = args.Session("cas-" + Guid.NewGuid().ToString("N")[..8]);
        var engine = new TurnEngine(settings);
        if (engine.Store.Load(Lab.Actor, id) is null)
            await engine.ExecuteAsync(id, "I use Windows 11 and VPN error 809.", Lab.DefaultPolicy);
        // Both independent requests deliberately load the same durable revision.
        var storeA = new ConversationStore(settings.DatabasePath);
        var storeB = new ConversationStore(settings.DatabasePath);
        var loadedA = storeA.Load(Lab.Actor, id)!;
        var loadedB = storeB.Load(Lab.Actor, id)!;
        string config = AgentFactory.ConfigurationKey(settings, Lab.DefaultPolicy.Version);
        if (loadedA.AgentConfig != config || loadedB.AgentConfig != config)
            throw new MemoryFault("config-mismatch");
        var candidateA = await MakeCandidate(loadedA, "Record check-in A.");
        var candidateB = await MakeCandidate(loadedB, "Record check-in B.");
        long revision = loadedA.Revision + 1;
        storeA.Save(Lab.Actor, id, loadedA.Revision, config, loadedA.ReadBusinessState<CaseState>(),
            candidateA.Session, Audit("winner", candidateA));
        string staleOutcome;
        try
        {
            storeB.Save(Lab.Actor, id, loadedB.Revision, config, loadedB.ReadBusinessState<CaseState>(),
                candidateB.Session, Audit("stale", candidateB));
            throw new MemoryFault("stale-write-was-accepted");
        }
        catch (MemoryFault ex) when (ex.Code == "version-conflict")
        {
            staleOutcome = ex.Code;
        }
        var current = storeA.Load(Lab.Actor, id)!;
        bool winnerPreserved = current.Revision == revision &&
            current.Session.GetRawText() == candidateA.Session.GetRawText();
        if (!winnerPreserved) throw new MemoryFault("winner-was-overwritten");
        return new
        {
            example = "v06", execution = settings.Execution, sessionId = id,
            loadedRevision = loadedA.Revision, winningRevision = current.Revision,
            rejectedCandidate = staleOutcome, winnerPreserved,
            audit = storeA.ReadAudit(Lab.Actor, id)
        };

        TurnAudit Audit(string label, Candidate candidate) => new(
            label + "-" + Guid.NewGuid().ToString("N"), revision,
            DateTimeOffset.UtcNow.ToString("O"), candidate.InputChars, candidate.OutputChars,
            candidate.DroppedTurns, candidate.ElapsedMs);
        async Task<Candidate> MakeCandidate(StoredConversation snapshot, string message)
        {
            var trace = new ToolTrace();
            var agent = AgentFactory.Create(settings,
                MemoryTools.Create(Lab.Actor, snapshot.ReadBusinessState<CaseState>(), trace));
            var session = await agent.DeserializeSessionAsync(snapshot.Session);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var response = await agent.RunAsync(message, session, cancellationToken: timeout.Token);
            var reduced = MemoryPolicy.Trim(session);
            var serialized = await agent.SerializeSessionAsync(session);
            return new(serialized, message.Length, response.Text.Length,
                reduced.DroppedTurns, timer.ElapsedMilliseconds);
        }
    }

    public static async Task<object> AuditingAsync(CourseSettings settings, Arguments args)
    {
        string id = args.Session(args.Has("--audit") ? "v04-context-" + settings.ModeSuffix :
            "audit-" + Guid.NewGuid().ToString("N")[..8]);
        var engine = new TurnEngine(settings);
        if (!args.Has("--audit"))
        {
            await engine.ExecuteAsync(id, "I use Windows 11 and VPN error 809.", Lab.DefaultPolicy);
            await engine.ExecuteAsync(id, "Which error did I report?", Lab.DefaultPolicy);
        }
        return new
        {
            example = "v07", execution = args.Has("--audit") ? "local-no-inference" : settings.Execution,
            sessionId = id, audit = engine.Store.ReadAudit(Lab.Actor, id),
            evidenceType = "metadata-only; latest ten committed turns"
        };
    }

    private sealed record Candidate(JsonElement Session, int InputChars, int OutputChars,
        int DroppedTurns, long ElapsedMs);
}
