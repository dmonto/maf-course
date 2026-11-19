return await Runner.RunAsync("v09", args, async c =>
{
    string directory = await c.DownloadBlobFixtureAsync();
    var documents = await Files.LoadKnowledgeAsync(directory, c.Token);
    string source = c.Live ? "assigned-azure-blob-container" : "local-fixture-copy";
    int chunks = documents.Sum(d => d.Chunks.Length);

    // Readable summary; the RESULT JSON line below stays last.
    Console.WriteLine($"Execution mode: {c.ExecutionMode}");
    Console.WriteLine($"Source: {source}");
    Console.WriteLine($"Directory: {directory}");
    Console.WriteLine($"Documents: {documents.Length}   Chunks: {chunks}   Indexed: no\n");
    foreach (var d in documents)
        Console.WriteLine($"  - {d.Title} ({d.DocumentId}, {d.SourceFile}, tenant {d.TenantId}, {d.Chunks.Length} chunk(s))");
    Console.WriteLine();

    Output.Result(new { executionMode = c.ExecutionMode, source,
        directory, documents = documents.Length, chunks, indexed = false });
});
