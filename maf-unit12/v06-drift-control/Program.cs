using System.Text.Json;
using Unit12.Common;

return await App.RunAsync(async () =>
{
    CliOptions cli = CliOptions.Parse(args, "baseline", "candidate");
    RunReport baseline, candidate;
    if (cli.Get("baseline") is string a && cli.Get("candidate") is string b)
    { baseline = Reports.Load(a); candidate = Reports.Load(b); }
    else
    {
        App.Require(cli.Values.Count == 0 && cli.Mode == "fixture", "Use fixture, or provide both --baseline and --candidate reports.");
        baseline = (await EvaluationRunner.RunAsync(new("v06", "fixture", "baseline"))).Report;
        candidate = (await EvaluationRunner.RunAsync(new("v06", "fixture-bad", "candidate"))).Report;
    }
    string[] signals = Comparisons.DriftSignals(baseline, candidate);
    string dir = Reports.ArtifactDirectory("v06");
    Directory.CreateDirectory(dir);
    string file = Path.Combine(dir, $"diagnosis-{Guid.NewGuid():N}.json");
    var diagnosis = new { baselineRunId = baseline.RunId, candidateRunId = candidate.RunId, signals,
        statisticalDetector = false, note = "Diagnostic evidence for human review; no causal or statistical drift claim." };
    await File.WriteAllTextAsync(file, JsonSerializer.Serialize(diagnosis, Cases.Json));
    Console.WriteLine(JsonSerializer.Serialize(diagnosis, Cases.Json));
    if (baseline.ExitCode == 2 || candidate.ExitCode == 2) return 2;
    return signals.Length == 0 ? 0 : 1;
});
