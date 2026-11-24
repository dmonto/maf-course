using System.Text.Json;
using Unit12.Common;

return await App.RunAsync(async () =>
{
    CliOptions cli = CliOptions.Parse(args, "baseline", "candidate");
    RunReport baseline, candidate;
    if (cli.Get("baseline") is string left && cli.Get("candidate") is string right)
    { baseline = Reports.Load(left); candidate = Reports.Load(right); }
    else
    {
        App.Require(cli.Values.Count == 0, "Provide both --baseline and --candidate reports.");
        App.Require(cli.Mode != "fixture-bad", "Use fixture for the controlled pair, or azure for assigned deployments.");
        string? a = null, b = null;
        ModelIdentity? aIdentity = null, bIdentity = null;
        if (cli.Mode == "azure")
        {
            a = UnitEnvironment.Required("AZURE_OPENAI_DEPLOYMENT_NAME");
            b = UnitEnvironment.Required("EVAL_CANDIDATE_DEPLOYMENT_NAME");
            App.Require(a != b, "Assign different baseline and candidate deployments.");
            aIdentity = new(UnitEnvironment.Optional("EVAL_BASELINE_MODEL_NAME"), UnitEnvironment.Optional("EVAL_BASELINE_MODEL_VERSION"));
            bIdentity = new(UnitEnvironment.Optional("EVAL_CANDIDATE_MODEL_NAME"), UnitEnvironment.Optional("EVAL_CANDIDATE_MODEL_VERSION"));
        }
        else Console.WriteLine("Controlled fixture comparison. These are not measured model rankings.");
        baseline = (await EvaluationRunner.RunAsync(new("v04", cli.Mode, "baseline", a, AgentIdentity: aIdentity))).Report;
        App.Require(baseline.ExitCode == 0, "The baseline is not accepted; inspect it before running the candidate.");
        candidate = (await EvaluationRunner.RunAsync(new("v04", cli.Mode == "azure" ? "azure" : "fixture-bad", "candidate", b, AgentIdentity: bIdentity))).Report;
    }
    Comparison comparison = Comparisons.Pair(baseline, candidate);
    string dir = Reports.ArtifactDirectory("v04");
    Directory.CreateDirectory(dir);
    string file = Path.Combine(dir, $"comparison-{Guid.NewGuid():N}.json");
    await File.WriteAllTextAsync(file, JsonSerializer.Serialize(comparison, Cases.Json));
    Console.WriteLine($"Comparison: {file}; accepted={comparison.Accepted}");
    if (candidate.ExitCode == 2) return 2;
    return comparison.Accepted ? 0 : 1;
});
