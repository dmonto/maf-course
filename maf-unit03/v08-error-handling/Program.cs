using Unit03.Common;

return await CourseApp.RunAsync(args, async context =>
{
    string selected = context.Args.Get("--case") ?? (context.Demo ? "all" : "business");
    string[] cases = selected == "all"
        ? ["business", "before-tool", "after-tool", "timeout", "empty", "loop"] : [selected];
    if (!context.Demo && cases.Any(x => x != "business"))
        throw new ConfigurationException("Use --demo for deterministic fault injection.");
    List<object> results = [];
    foreach (string scenario in cases)
    {
        var fault = scenario switch
        {
            "business" => SyntheticFault.None,
            "before-tool" => SyntheticFault.BeforeTool,
            "after-tool" => SyntheticFault.AfterTool,
            "timeout" => SyntheticFault.Timeout,
            "empty" => SyntheticFault.EmptyResponse,
            "loop" => SyntheticFault.RepeatedToolCalls,
            _ => throw new ConfigurationException("Use business, before-tool, after-tool, timeout, empty, loop or all.")
        };
        using var host = await AgentHost.CreateAsync(context, fault: fault);
        string originalConversation = host.ConversationId;
        string prompt = scenario == "business" ? "What is the current status of CRM?" : "What is the current status of payroll?";
        var report = await host.RunTurnAsync(prompt,
            scenario == "timeout" ? TimeSpan.FromMilliseconds(75) : null);
        Console.WriteLine($"Case: {scenario}; outcome: {report.Status}; tool attempts: {report.ToolAttempts}");
        Console.WriteLine($"Agent: {report.Answer}");
        bool reset = originalConversation != host.ConversationId;
        results.Add(new { scenario, conversationReset = reset, report });
        if (context.Demo)
        {
            string expected = scenario switch { "timeout" => "timed-out", "business" or "loop" => "completed", _ => "failed" };
            if (report.Status != expected) throw new InvalidOperationException("Unexpected synthetic fault outcome.");
            if (expected != "completed" && !reset) throw new InvalidOperationException("Interrupted session was reused.");
        }
        else if (report.Status != "completed") throw new InvalidOperationException("Live invocation failed.");
    }
    context.Save("v08-error-handling", results);
});
