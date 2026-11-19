return await Runner.RunAsync("v10", args, async c =>
{
    string service = c.Options.Get("--service", "vpn");
    if (service is not ("vpn" or "payroll")) throw new ArgumentException("Use vpn or payroll.");
    string profile = c.Options.Get("--profile", "employee");
    // This status catalog is synthetic in both execution modes; only knowledge uses Azure.
    string status = service == "vpn" ? "healthy" : "degraded";
    var knowledge = await c.AnswerAsync(profile, $"What guidance applies to {service}?");
    string runId = Guid.NewGuid().ToString("N");
    bool requiresHumanReview = status == "degraded";
    string nextStep = requiresHumanReview ? "Prepare a proposal for an authorized reviewer." : "Inspect authorized troubleshooting guidance.";

    // Readable summary; the RESULT JSON line below stays last.
    var json = System.Text.Json.JsonSerializer.SerializeToElement(knowledge, Output.Json);
    Console.WriteLine($"Run: {runId}   Execution mode: {c.ExecutionMode}   Catalog version: {c.Settings.CatalogVersion}");
    Console.WriteLine($"Service: {service}   Profile: {profile}");
    Console.WriteLine($"Status: {status} (source: synthetic-status-catalog)");
    Console.WriteLine($"Human review required: {(requiresHumanReview ? "yes" : "no")}   External action executed: no");
    Console.WriteLine($"Next step: {nextStep}");
    Console.WriteLine("\nKnowledge answer:");
    foreach (string line in json.GetProperty("answer").GetString()!.Split('\n'))
        Console.WriteLine("  " + line);
    var evidence = json.GetProperty("evidence").EnumerateArray().ToArray();
    Console.WriteLine($"\nEvidence ({evidence.Length}):");
    if (evidence.Length == 0) Console.WriteLine("    (no evidence visible)");
    foreach (var e in evidence)
        Console.WriteLine($"    - [{e.GetProperty("score").GetDouble():F3}] {e.GetProperty("title").GetString()} ({e.GetProperty("documentId").GetString()})");
    Console.WriteLine();

    Output.Result(new { runId, executionMode = c.ExecutionMode,
        service, status, statusSource = "synthetic-status-catalog", catalogVersion = c.Settings.CatalogVersion,
        knowledge, requiresHumanReview, externalActionExecuted = false, nextStep });
});
