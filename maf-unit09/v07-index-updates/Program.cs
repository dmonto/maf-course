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
    Output.Result(new { executionMode = c.ExecutionMode, servingVersion = c.Settings.CatalogVersion, stagedVersion = version,
        active, candidate, promoted = false, stagingDirectory = directory });
});
