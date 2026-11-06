return await Runner.RunAsync("v03", args, async c =>
{
    string question = c.Options.Get("--question", "Windows 11 VPN error 809");
    var modes = new List<object>();
    foreach (string mode in new[] { "keyword", "vector", "hybrid" })
    {
        var hits = await c.SearchAsync("employee", mode, question);
        modes.Add(new { mode, evidence = hits });
    }
    Output.Result(new { executionMode = c.ExecutionMode, modes, note = "Scores from different retrieval modes are not directly comparable." });
});
