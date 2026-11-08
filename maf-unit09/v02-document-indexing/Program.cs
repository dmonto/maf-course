return await Runner.RunAsync("v02", args, async c =>
{
    // This is the only ordinary example that writes the v1 fixture to Azure.
    await c.Store().SeedAsync(c.Documents, c.Token);
    Output.Result(new { documents = c.Documents.Length, chunks = c.Documents.Sum(d => d.Chunks.Length),
        c.Settings.CatalogVersion, schemaVerified = !c.Live, executionMode = c.ExecutionMode });
});
