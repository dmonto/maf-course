using Unit02.Common;

return await CourseApp.RunAsync(args, async context =>
{
    var baseline = context.Connect("azure", context.Model("CHAT_FAST_DEPLOYMENT", "demo-baseline"));
    var candidate = context.Connect("azure", context.Model("CHAT_QUALITY_DEPLOYMENT", "demo-candidate"));
    if (baseline.Model == candidate.Model) throw new ConfigurationException("Baseline and candidate must use distinct deployments.");
    var baselineRuns = new List<RunRecord>();
    var candidateRuns = new List<RunRecord>();
    foreach (var incident in Tasks.Incidents)
    {
        foreach (var (connection, destination) in new[] { (baseline, baselineRuns), (candidate, candidateRuns) })
        {
            // Deliberately introduce one synthetic regression so demo mode exercises rejection.
            string answer = context.Demo && connection == candidate && incident.Id == "INC-003"
                ? "Service: Inventory\nImpact: Stock synchronization is delayed.\nNext step: Recovery in 15 minutes."
                : incident.DemoAnswer;
            var result = await Inference.RunAsync(context, connection, Tasks.SummaryInstructions,
                incident.Prompt, answer, text => Tasks.CheckSummary(text, incident));
            Inference.Print(result);
            destination.Add(result.Record);
        }
    }
    object Metadata(string prefix, Connection connection) => new {
        connection.Model,
        modelFamily = context.Setting(prefix + "_MODEL_FAMILY") ?? (context.Demo ? "synthetic" : "unrecorded"),
        modelVersion = context.Setting(prefix + "_MODEL_VERSION") ?? (context.Demo ? "demo-v1" : "unrecorded"),
        upgradePolicy = context.Setting(prefix + "_UPGRADE_POLICY") ?? (context.Demo ? "synthetic" : "unrecorded")
    };
    bool metadataComplete = context.Demo || new[] { "BASELINE", "CANDIDATE" }.All(prefix =>
        new[] { "_MODEL_FAMILY", "_MODEL_VERSION", "_UPGRADE_POLICY" }.All(suffix => context.Setting(prefix + suffix) is not null));
    bool qualityPassed = baselineRuns.Concat(candidateRuns).All(r => r.Status == "ok" && r.QualityPassed);
    var report = new {
        applicationCommit = context.Setting("APPLICATION_COMMIT"), instructionsVersion = context.Setting("INSTRUCTIONS_VERSION"),
        baseline = Metadata("BASELINE", baseline), candidate = Metadata("CANDIDATE", candidate),
        metadataComplete, qualityPassed,
        recommendation = qualityPassed && metadataComplete ? "candidate-eligible-for-review" : "keep-baseline",
        automaticDeploymentChange = false, baselineRuns, candidateRuns
    };
    context.Save("v06-regression.json", report);
    Json.Print(report);
    Console.WriteLine("The gate checks this small fact set; it does not establish production safety or alter deployments.");
    if (baselineRuns.Concat(candidateRuns).Any(r => r.Status != "ok")) throw new InvalidOperationException();
});
