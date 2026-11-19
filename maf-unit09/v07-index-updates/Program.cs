return await Runner.RunAsync("v07", args, async c =>
{
    string version = c.Options.Get("--version", "v2");
    string directory = await c.StageAsync(version);
    var staged = await Files.LoadKnowledgeAsync(directory, c.Token);
    await c.Store().SeedAsync(staged, c.Token);
    var active = await c.SearchAsync("employee", "hybrid", "VPN error 809");
    var candidate = await c.SearchAsync("employee", "hybrid", "VPN error 809", version);
    Output.Require(active.All(h => h.CatalogVersion == c.Settings.CatalogVersion), "Active retrieval crossed catalog versions.");
    Output.Require(candidate.All(h => h.CatalogVersion == version), "Staged retrieval crossed catalog versions.");

    // Readable summary; the RESULT JSON line below stays last.
    Console.WriteLine($"Execution mode: {c.ExecutionMode}");
    Console.WriteLine($"Serving version: {c.Settings.CatalogVersion}   Staged version: {version}   Promoted: no");
    Console.WriteLine($"Staging directory: {directory}");
    Console.WriteLine($"Staged documents: {staged.Length}");
    static void Show(string label, SearchHit[] hits)
    {
        Console.WriteLine($"\n== {label} ({hits.Length} hit(s)) for \"VPN error 809\" ==");
        if (hits.Length == 0) Console.WriteLine("    (no evidence visible)");
        foreach (var h in hits)
            Console.WriteLine($"    - [{h.CatalogVersion}] {h.Title} ({h.DocumentId})");
    }
    Show($"Active index ({c.Settings.CatalogVersion})", active);
    Show($"Candidate index ({version})", candidate);
    Console.WriteLine("\nChecks:");
    Console.WriteLine("  PASS  Active retrieval stays on the serving version");
    Console.WriteLine("  PASS  Staged retrieval stays on the staged version\n");

    Output.Result(new { executionMode = c.ExecutionMode, servingVersion = c.Settings.CatalogVersion, stagedVersion = version,
        active, candidate, promoted = false, stagingDirectory = directory });
});
