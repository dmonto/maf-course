using Unit13;
using System.Text.Json;
using System.Security.Claims;

return await Entry.RunAsync("v09", args, async context =>
{
    context.FixtureOnly();
    string? supplied = context.Options.Get("--report");
    if (supplied is not null) return await PythonGate.RunAsync(context.UnitRoot, supplied);
    int suiteExit = await AgentSuite.RunAsync(context);
    if (suiteExit != 0) return suiteExit;
    int accepted = await PythonGate.RunAsync(context.UnitRoot, context.Artifacts);
    if (accepted != 0) return accepted;
    // Corrupt only a newly-created copy, preserving the original evidence.
    string bad = Path.Combine(context.Artifacts, "negative-policy-copy");
    Directory.CreateDirectory(bad);
    File.Copy(Path.Combine(context.Artifacts, "results.json"), Path.Combine(bad, "results.json"));
    string audit = File.ReadAllText(Path.Combine(context.Artifacts, "audit.jsonl"));
    File.WriteAllText(Path.Combine(bad, "audit.jsonl"), audit.Replace("security-v1", "unreviewed-policy", StringComparison.Ordinal));
    int rejected = await PythonGate.RunAsync(context.UnitRoot, bad);
    Lab.Require(rejected == 1, "The unreviewed policy must fail the gate.");
    context.Save("gate-demonstration.json", new { positiveExit = accepted, expectedNegativeExit = rejected });
    Lab.Show(new { validEvidenceAccepted = true, unreviewedPolicyRejected = true });
    return 0;
});
