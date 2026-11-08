using System.Text.Json;
return await Runner.RunAsync("ex02", args, async c =>
{
    var cases = new[]
    {
        new EvaluationCase("vpn-paraphrase", "employee", "My Windows tunnel fails with 809; which diagnostics should I gather?", "vpn-guide", [], false),
        new EvaluationCase("payroll-denied", "employee", "Who may retry a payroll batch?", null, ["payroll-guide", "vpn-tenant-b"], false),
        new EvaluationCase("payroll-allowed", "payroll", "Who may retry a payroll batch?", "payroll-guide", ["vpn-tenant-b"], false),
        new EvaluationCase("tenant-marker", "employee", "BRAVO-CANARY", null, ["vpn-tenant-b"], false),
        new EvaluationCase("empty-permissions", "none", "VPN error 809", null, ["vpn-guide", "payroll-guide", "password-guide", "vpn-tenant-b"], true)
    };
    var rows = new List<object>(); int failures = 0, hits = 0, positives = 0, violations = 0;
    foreach (var item in cases)
    {
        var evidence = await c.SearchAsync(item.Profile, "hybrid", item.Question);
        var ids = evidence.Select(h => h.DocumentId).Distinct().ToArray();
        string? expected = c.Options.Has("--force-failure") && item.Id == "vpn-paraphrase" ? "deliberately-missing-document" : item.Expected;
        bool found = expected is null || ids.Contains(expected);
        int leaked = ids.Count(id => item.Forbidden.Contains(id));
        bool pass = found && leaked == 0 && (!item.Empty || evidence.Length == 0);
        if (expected is not null) { positives++; if (found) hits++; }
        violations += leaked; if (!pass) failures++;
        rows.Add(new { queryId = item.Id, item.Profile, returnedDocumentIds = ids, expectedDocumentId = expected,
            hitAt3 = expected is null ? (bool?)null : found, authorizationViolations = leaked,
            catalogVersion = c.Settings.CatalogVersion, pass });
    }
    var report = new { executionMode = c.ExecutionMode, catalogVersion = c.Settings.CatalogVersion,
        embeddingDeployment = c.Settings.EmbeddingDeployment, cases = rows, retrievalHitRate = positives == 0 ? 0 : (double)hits / positives,
        authorizationViolations = violations, failures, pass = failures == 0, modelInvoked = false };
    string directory = Path.Combine(c.Root, "reports"); Directory.CreateDirectory(directory);
    await File.WriteAllTextAsync(Path.Combine(directory, "exercise02-regressions.json"), JsonSerializer.Serialize(report, Output.Json), c.Token);
    Output.Result(report);
    if (failures > 0) throw new InvalidOperationException("Retrieval regression expectations failed; inspect the JSON report.");
});
internal sealed record EvaluationCase(string Id, string Profile, string Question, string? Expected, string[] Forbidden, bool Empty);
