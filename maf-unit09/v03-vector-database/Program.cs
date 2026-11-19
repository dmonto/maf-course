return await Runner.RunAsync("v03", args, async c =>
{
    string question = c.Options.Get("--question", "Windows 11 VPN error 809");
    var modes = new List<object>();
    var byMode = new Dictionary<string, SearchHit[]>();
    foreach (string mode in new[] { "keyword", "vector", "hybrid" })
    {
        var hits = await c.SearchAsync("employee", mode, question);
        byMode[mode] = hits;
        modes.Add(new { mode, evidence = hits });
    }
    Console.WriteLine($"Question: {question}  (execution mode: {c.ExecutionMode})");
    foreach (string mode in new[] { "keyword", "vector", "hybrid" })
    {
        var hits = byMode[mode];
        Console.WriteLine($"\n== {mode.ToUpperInvariant()} ({hits.Length} hits) ==");
        for (int i = 0; i < hits.Length; i++)
        {
            string text = hits[i].Content.ReplaceLineEndings(" ");
            if (text.Length > 100) text = text[..100] + "...";
            Console.WriteLine($"{i + 1,2}. [{hits[i].Score:F3}] {hits[i].Title} ({hits[i].DocumentId})");
            Console.WriteLine($"    {text}");
        }
    }
    Console.WriteLine("\nNote: scores from different retrieval modes are not directly comparable.\n");
    Output.Result(new { executionMode = c.ExecutionMode, modes, note = "Scores from different retrieval modes are not directly comparable." });
});
