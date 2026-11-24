using Unit13;
using System.Text.Json;

return await Entry.RunAsync("checks", args, context =>
{
    context.FixtureOnly();
    string[] bad = ["", "../vpn", new string('a', 33), "vpn\nmessage", "VPN"];
    foreach (string service in bad) Lab.Require(!Authorization.ValidResource(service), "Invalid tool argument accepted.");
    MemoryAuditSink audit = new();
    Caller current = Fixtures.Reader;
    SecurityTools bound = new(() => current, "vpn", "identity-change", audit);
    current = current with { Subject = "different-user" };
    Lab.Require(bound.GetServiceStatus("vpn").Status == "forbidden" && audit.Events[0].Reason == "caller_context_changed", "Bound identity changed.");
    MemoryAuditSink malformedAudit = new();
    SecurityTools malformed = new(() => Fixtures.Reader, "vpn", "invalid-resource", malformedAudit);
    malformed.GetServiceStatus("owner@example.invalid");
    Lab.Require(malformedAudit.Events[0].Resource == "invalid", "Untrusted audit resource retained.");
    string temp = Path.Combine(context.Artifacts, "literal.env");
    File.WriteAllText(temp, "UNIT13_CHECK_LITERAL='$(must-not-execute)'\nUNIT13_CHECK_PRECEDENCE=file\n");
    Environment.SetEnvironmentVariable("UNIT13_CHECK_PRECEDENCE", "process");
    try
    {
        EnvironmentFile.Load(temp);
        Lab.Require(Environment.GetEnvironmentVariable("UNIT13_CHECK_LITERAL") == "$(must-not-execute)", "Environment value was evaluated.");
        Lab.Require(Environment.GetEnvironmentVariable("UNIT13_CHECK_PRECEDENCE") == "process", "Environment precedence changed.");
    }
    finally
    {
        Environment.SetEnvironmentVariable("UNIT13_CHECK_LITERAL", null);
        Environment.SetEnvironmentVariable("UNIT13_CHECK_PRECEDENCE", null);
    }
    bool traversalRejected = false;
    try { Retention.ApplyToOwnedLabFiles(context.Artifacts, [new("../outside.json", "audit", DateTimeOffset.MinValue, false)], DateTimeOffset.UtcNow); }
    catch (ArgumentException) { traversalRejected = true; }
    Lab.Require(traversalRejected, "Retention escaped owned files.");
    context.Save("verification.json", new { passed = true, checks = new[]
        { "invalid arguments", "bound identity", "audit resource sanitization", "literal env", "env precedence", "retention path scope" } });
    Lab.Show(new { verification = "PASS" });
    return Task.FromResult(0);
});
