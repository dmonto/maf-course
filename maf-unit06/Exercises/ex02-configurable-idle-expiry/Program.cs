using Unit06.Common;
using Unit06.Exercise02;

return await ConsoleApp.RunAsync(args, async (settings, options) =>
{
    // Validate the host configuration before creating an agent or invoking a model.
    TimeSpan lifetime = IdlePolicy.Read(CourseSettings.Setting("MAF_MEMORY_IDLE_SECONDS"));
    var policy = Lab.DefaultPolicy with { Version = "exercise02-idle-expiry-v1", IdleLifetime = lifetime };
    string id = options.Session("ex02-expiry-" + settings.ModeSuffix);
    if (options.Has("--verify-expiry"))
    {
        if (!settings.Demo)
            throw new ConfigurationException("The controlled-clock verification requires --demo.");
        id = "expiry-" + Guid.NewGuid().ToString("N")[..8];
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var engine = new TurnEngine(settings, clock);
        var first = await engine.ExecuteAsync(id, "I use Windows 11 and VPN error 809.", policy);
        var before = engine.Store.Load(Lab.Actor, id)!;
        int auditCount = engine.Store.ReadAudit(Lab.Actor, id).Count;
        long calls = ScriptedMemoryClient.TotalCalls;
        clock.Advance(lifetime + TimeSpan.FromSeconds(1));
        string outcome;
        try
        {
            await engine.ExecuteAsync(id, "Which error did I report?", policy);
            throw new MemoryFault("expired-session-was-restored");
        }
        catch (MemoryFault ex) when (ex.Code == "memory-expired") { outcome = ex.Code; }
        bool noModelCall = ScriptedMemoryClient.TotalCalls == calls;
        bool noAuditAdded = engine.Store.ReadAudit(Lab.Actor, id).Count == auditCount;
        // Read the old row at the original time to verify its revision was not changed.
        var originalClock = new ManualClock(DateTimeOffset.FromUnixTimeSeconds(before.ExpiresUnix)
            .Subtract(TimeSpan.FromSeconds(1)));
        long revision = new ConversationStore(settings.DatabasePath, originalClock)
            .Load(Lab.Actor, id)!.Revision;
        if (!noModelCall || !noAuditAdded || revision != first.Revision)
            throw new MemoryFault("expiry-contract-failed");
        return (object)new
        {
            exercise = "ex02", execution = "demo-scripted-model",
            clock = "explicit-controlled-test-clock", sessionId = id,
            idleSeconds = lifetime.TotalSeconds, expiredOutcome = outcome,
            noModelCallAfterExpiry = noModelCall, noAuditAdded, unchangedRevision = revision,
            physicalDeletion = false
        };
    }
    var durable = new TurnEngine(settings);
    if (options.Has("--audit")) return (object)new { sessionId = id,
        execution = "local-no-inference", audit = durable.Store.ReadAudit(Lab.Actor, id) };
    return await durable.ExecuteAsync(id, options.Input(
        "I use Windows 11 and VPN error 809."), policy);
}, extraFlags: ["--verify-expiry"]);
