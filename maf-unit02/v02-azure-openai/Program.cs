using Unit02.Common;

return await CourseApp.RunAsync(args, async context =>
{
    string fast = context.Model("CHAT_FAST_DEPLOYMENT", "demo-chat-fast");
    string quality = context.Model("CHAT_QUALITY_DEPLOYMENT", "demo-chat-quality");
    if (fast == quality) throw new ConfigurationException("Use two distinct assigned deployments.");
    var incident = Tasks.Incidents[0];
    var runs = new List<RunRecord>();
    foreach (var deployment in new[] { fast, quality })
    {
        var connection = context.Connect("azure", deployment);
        var result = await Inference.RunAsync(context, connection, Tasks.SummaryInstructions,
            incident.Prompt, incident.DemoAnswer, text => Tasks.CheckSummary(text, incident));
        Inference.Print(result);
        runs.Add(result.Record);
    }
    context.Save("v02-deployments.json", runs);
    Console.WriteLine("Compare required facts, latency and reported usage; two calls are not a benchmark.");
    if (runs.Any(r => r.Status != "ok")) throw new InvalidOperationException();
});
