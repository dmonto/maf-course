using System.Text.Json;

return await Runner.RunAsync("v04", args, async c =>
{
    string profile = c.Options.Get("--profile", "employee");
    string mode = c.Options.Get("--mode", "hybrid");
    string question = c.Options.Get("--question", "What is the recovery time for VPN error 809?");
    var result = await c.AnswerAsync(profile, question, mode);

    // Readable summary; the RESULT JSON line below stays last.
    var json = JsonSerializer.SerializeToElement(result, Output.Json);
    Console.WriteLine($"Question: {question}");
    Console.WriteLine($"Profile: {profile}  Mode: {mode}  Execution: {json.GetProperty("executionMode")}  " +
        $"Model invoked: {json.GetProperty("modelInvoked")}  Retrieval: {json.GetProperty("retrievalMs")} ms");
    Console.WriteLine("\nAnswer:");
    foreach (string line in json.GetProperty("answer").GetString()!.Split('\n'))
        Console.WriteLine("  " + line);
    var evidence = json.GetProperty("evidence").EnumerateArray().ToArray();
    Console.WriteLine($"\nEvidence ({evidence.Length}):");
    for (int i = 0; i < evidence.Length; i++)
    {
        string text = evidence[i].GetProperty("content").GetString()!.ReplaceLineEndings(" ");
        if (text.Length > 100) text = text[..100] + "...";
        Console.WriteLine($"{i + 1,2}. [{evidence[i].GetProperty("score").GetDouble():F3}] " +
            $"{evidence[i].GetProperty("title").GetString()} ({evidence[i].GetProperty("documentId").GetString()})");
        Console.WriteLine($"    {text}");
    }
    Console.WriteLine();
    Output.Result(result);
});
