return await Runner.RunAsync("v09", args, async c =>
{
    string directory = await c.DownloadBlobFixtureAsync();
    var documents = await Files.LoadKnowledgeAsync(directory, c.Token);
    Output.Result(new { executionMode = c.ExecutionMode, source = c.Live ? "assigned-azure-blob-container" : "local-fixture-copy",
        directory, documents = documents.Length, chunks = documents.Sum(d => d.Chunks.Length), indexed = false });
});
