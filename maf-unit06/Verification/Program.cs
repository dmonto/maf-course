using System.Text.Json;
using Microsoft.Data.Sqlite;
using Unit06.Common;
using Unit06.Exercise02;

return await ConsoleApp.RunAsync(args, async (settings, options) =>
{
    int checks = 0;
    void Check(bool ok, string name)
    {
        if (!ok) throw new MemoryFault("check-failed-" + name);
        checks++;
    }
    void Fault(Action action, string expected)
    {
        try { action(); throw new MemoryFault("expected-fault-" + expected); }
        catch (MemoryFault ex) when (ex.Code == expected) { checks++; }
    }
    var local = settings with { Demo = true };
    string path = Path.Combine(settings.Root, "output", "verification-" + Guid.NewGuid().ToString("N") + ".db");
    var clock = new ManualClock(DateTimeOffset.UtcNow);
    var store = new ConversationStore(path, clock);
    var agent = AgentFactory.Create(local, []);
    var session = await agent.CreateSessionAsync();
    await agent.RunAsync("My test marker is copper-51. I use Windows 11 and VPN error 809.", session);
    var snapshot = await agent.SerializeSessionAsync(session);
    var audit = new TurnAudit("seed", 1, clock.GetUtcNow().ToString("O"), 72, 50, 0, 1);
    store.Save(Lab.Actor, "boundary-test", 0, "test-config", Lab.NewCase(), snapshot, audit);
    var before = store.Load(Lab.Actor, "boundary-test")!;
    Check(before.Revision == 1 && before.ReadBusinessState<CaseState>() == Lab.NewCase(), "roundtrip");
    Check(store.Load(new("lab-tenant", "other-owner"), "boundary-test") is null, "owner-isolation");
    Check(store.Load(new("other-tenant", "student-001"), "boundary-test") is null, "tenant-isolation");
    Check(store.ReadAudit(new("lab-tenant", "other-owner"), "boundary-test").Count == 0, "audit-isolation");
    Check(store.Load(Lab.Actor, "' OR 1=1 --") is null, "parameterized-key");
    Fault(() => store.Save(Lab.Actor, "boundary-test", 0, "test-config", Lab.NewCase(), snapshot,
        audit with { RunId = "duplicate-new" }), "version-conflict");
    try
    {
        store.Save(Lab.Actor, "boundary-test", 1, "test-config", Lab.NewCase(), snapshot,
            audit with { Revision = 2 });
        throw new MemoryFault("duplicate-audit-was-accepted");
    }
    catch (SqliteException) { checks++; }
    Check(store.Load(Lab.Actor, "boundary-test")!.Revision == 1, "audit-rollback-revision");
    Check(store.Load(Lab.Actor, "boundary-test")!.Session.GetRawText() == before.Session.GetRawText(),
        "audit-rollback-snapshot");
    Check(store.ReadAudit(Lab.Actor, "boundary-test").Count == 1, "audit-rollback-row-count");
    var denied = new MemoryTools(new("lab-tenant", "other-owner"), Lab.NewCase(), new());
    Check(JsonSerializer.Serialize(denied.GetCaseContext()).Contains("unavailable"), "tool-owner-check");
    var wrongCase = new MemoryTools(Lab.Actor, new("OTHER", "vpn", "investigating"), new());
    Check(JsonSerializer.Serialize(wrongCase.GetCaseContext()).Contains("unavailable"), "tool-case-check");
    foreach (string invalid in new[] { "0", "-1", "text", "86401" })
    {
        try { IdlePolicy.Read(invalid); throw new MemoryFault("ttl-was-accepted"); }
        catch (ConfigurationException) { checks++; }
    }
    Check(IdlePolicy.Read(null) == TimeSpan.FromHours(24), "default-ttl");
    Check(IdlePolicy.Read("60") == TimeSpan.FromSeconds(60), "minimum-ttl");
    clock.Advance(TimeSpan.FromHours(25));
    Fault(() => store.Load(Lab.Actor, "boundary-test"), "memory-expired");
    Check(store.ReadAudit(Lab.Actor, "boundary-test").Count == 1, "expiry-keeps-audit");
    using (var connection = new SqliteConnection("Data Source=" + path))
    {
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version=2";
        command.ExecuteNonQuery();
    }
    Fault(() => new ConversationStore(path), "schema-mismatch");
    return (object)new { outcome = "passed", execution = "local-no-Azure",
        checks, storage = "real-SQLite", inference = "scripted-MAF-client" };
});
