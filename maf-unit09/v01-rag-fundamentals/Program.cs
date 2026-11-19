using System.Text.Json;

return await Runner.RunAsync("v01", args, async c =>
{
    string question = c.Options.Get("--question", "What should I collect for Windows 11 VPN error 809?");
    string profile = c.Options.Get("--profile", "employee");
    var result = await c.AnswerAsync(profile, question);

    // Readable summary; the RESULT JSON line below stays last.
    var json = JsonSerializer.SerializeToElement(result, Output.Json);
    Console.WriteLine($"Question: {question}");
    Console.WriteLine($"Profile: {profile}  Execution: {json.GetProperty("executionMode")}  " +
        $"Model invoked: {json.GetProperty("modelInvoked")}  Retrieval: {json.GetProperty("retrievalMs")} ms");
    Console.WriteLine("\nAnswer:");
    foreach (string line in json.GetProperty("answer").GetString()!.Split('\n'))
        Console.WriteLine("  " + line);
    var evidence = json.GetProperty("evidence").EnumerateArray().ToArray();
    Console.WriteLine($"\nEvidence ({evidence.Length}):");
    if (evidence.Length == 0) Console.WriteLine("    (no evidence visible)");
    foreach (var e in evidence)
        Console.WriteLine($"    - [{e.GetProperty("score").GetDouble():F3}] {e.GetProperty("title").GetString()} ({e.GetProperty("documentId").GetString()})");
    Console.WriteLine();

    Output.Result(result);
});
