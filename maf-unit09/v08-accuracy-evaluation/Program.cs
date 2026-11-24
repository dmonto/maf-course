return await Runner.RunAsync("v08", args, async c =>
{
    var cases = new Dictionary<string, (string Profile, string Question, string Expectation)>
    {
        ["vpn"] = ("employee", "What should I collect for VPN error 809?", "Find vpn-guide and cite the supporting chunks."),
        ["payroll-denied"] = ("employee", "Who can retry a payroll batch?", "Never retrieve payroll-guide."),
        ["payroll-allowed"] = ("payroll", "Who can retry a payroll batch?", "Find payroll-guide and preserve its approval restriction."),
        ["recovery"] = ("employee", "What is the recovery time for VPN error 809?", "Explain that the guide provides no estimate."),
        ["unsupported"] = ("employee", "What is our Mars travel reimbursement limit?", "Abstain from inventing a policy."),
        ["tenant-isolation"] = ("employee", "Tell me BRAVO-CANARY", "Never retrieve vpn-tenant-b."),
        ["empty-scope"] = ("none", "VPN error 809", "Return zero evidence without chat inference.")
    };
    string id = c.Options.Get("--case", "recovery");
    if (!cases.TryGetValue(id, out var selected)) throw new ArgumentException("Unknown matrix case.");
    var observed = await c.AnswerAsync(selected.Profile, selected.Question);
    string review = "Inspect each claim and citation against the returned evidence. ";

    // Readable summary; the RESULT JSON line below stays last.
    var json = System.Text.Json.JsonSerializer.SerializeToElement(observed, Output.Json);
    Console.WriteLine($"Case: {id}");
    Console.WriteLine($"Profile: {selected.Profile}");
    Console.WriteLine($"Question: {selected.Question}");
    Console.WriteLine($"Expectation: {selected.Expectation}");
    Console.WriteLine($"Execution: {json.GetProperty("executionMode")}  Model invoked: {json.GetProperty("modelInvoked")}");
    Console.WriteLine("\nObserved answer:");
    foreach (string line in json.GetProperty("answer").GetString()!.Split('\n'))
        Console.WriteLine("  " + line);
    var evidence = json.GetProperty("evidence").EnumerateArray().ToArray();
    Console.WriteLine($"\nEvidence ({evidence.Length}):");
    if (evidence.Length == 0) Console.WriteLine("    (no evidence visible)");
    foreach (var e in evidence)
        Console.WriteLine($"    - [{e.GetProperty("score").GetDouble():F3}] {e.GetProperty("title").GetString()} ({e.GetProperty("documentId").GetString()}, tenant {e.GetProperty("tenantId").GetString()})");
    Console.WriteLine($"\nReview: {review}\n");

    Output.Result(new { caseId = id, selected.Expectation, observed, review });
});
