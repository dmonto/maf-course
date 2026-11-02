using Unit02.Common;

return await CourseApp.RunAsync(args, async context =>
{
    var connection = context.Connect();
    int count = context.Number("OBSERVABILITY_RUNS", 3, 1, 100);
    var incident = Tasks.Incidents[0];
    var runs = new List<RunRecord>();
    for (int i = 0; i < count; i++)
    {
        var result = await Inference.RunAsync(context, connection, Tasks.SummaryInstructions,
            incident.Prompt, incident.DemoAnswer, text => Tasks.CheckSummary(text, incident),
            i == 0 ? "first-request" : "subsequent-request");
        Inference.Print(result);
        runs.Add(result.Record);
    }
    var success = runs.Where(r => r.Status == "ok").ToArray();
    var report = new {
        mode = context.Demo ? "demo" : "live", sampleCount = runs.Count,
        successfulConnections = success.Length, qualityPasses = runs.Count(r => r.QualityPassed),
        p50Ms = success.Length > 0 ? (double?)Statistics.Percentile(success.Select(r => r.ElapsedMs), 0.50) : null,
        p95Ms = success.Length > 0 ? (double?)Statistics.Percentile(success.Select(r => r.ElapsedMs), 0.95) : null,
        interpretation = context.Demo ? "Synthetic timings; not a performance measurement" : "Small connection sample; not a representative performance benchmark",
        timingBoundary = "client-request", rawContentPersisted = false, exporterConfigured = false,
        runs
    };
    context.Save("v08-observability.json", report);
    Json.Print(report);
    if (runs.Any(r => r.Status != "ok")) throw new InvalidOperationException();
});
