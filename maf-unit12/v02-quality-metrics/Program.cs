using System.Text.Json;
using Microsoft.Extensions.AI.Evaluation;
using Unit12.Common;

return await App.RunAsync(async () =>
{
    CliOptions cli = CliOptions.Parse(args);
    var samples = new (string Name, NumericMetric? Metric, bool Valid)[]
    {
        ("valid-test-value", new("Groundedness", 4, "Synthetic validator input; not a judge measurement."), true),
        ("missing-metric", null, false), ("missing-value", new("Groundedness", null), false),
        ("out-of-range", new("Groundedness", 6), false), ("not-finite", new("Groundedness", double.NaN), false)
    };
    foreach (var sample in samples)
    {
        Score score = MetricValidation.Read(sample.Metric);
        App.Require(score.Valid == sample.Valid, $"Metric validation failed for {sample.Name}.");
        Console.WriteLine($"Validator test: {sample.Name}; valid={score.Valid}; syntheticInput=true");
    }
    var tools = new SupportTools();
    Evidence e = tools.GetServiceStatus("vpn");
    string unsupported = JsonSerializer.Serialize(new Answer("vpn", e.Status, false,
        "MFA errors are reported. The service will recover in fifteen minutes."), Cases.Json);
    var exact = Contract.Check(Cases.All[0], unsupported, tools.Calls);
    App.Require(exact.Values.All(x => x), "The demonstration's exact contract should pass.");
    Console.WriteLine("Unsupported recovery text passes the base exact checks. Its semantic quality has not been measured.");
    RunResult result = await EvaluationRunner.RunAsync(new("v02", cli.Mode));
    Console.WriteLine("Azure mode measures relevance and grounding for the four standard cases; fixture mode does not run a judge.");
    Reports.Print(result);
    foreach (Outcome o in result.Report.Outcomes.Where(x => !x.Passed))
    {
        Console.WriteLine($"FAILURE DETAIL {o.CaseId}/{o.Iteration}:");
        if (o.Error is not null) Console.WriteLine($"  runtime error: {o.Error}");
        foreach (string check in o.Checks.Where(x => !x.Value).Select(x => x.Key))
            Console.WriteLine($"  contract check failed: {check}");
        foreach (string metric in Contract.RequiredMetrics)
        {
            if (!o.JudgeRequired) break;
            if (!o.Scores.TryGetValue(metric, out Score? s))
                Console.WriteLine($"  {metric}: no score recorded (judge did not run or errored before scoring)");
            else if (!s.Valid)
                Console.WriteLine($"  {metric}: INVALID score (value={s.Value?.ToString() ?? "null"}); reason: {s.Reason ?? "none"}");
            else if (s.Value < 4)
                Console.WriteLine($"  {metric}: score {s.Value} is below the minimum 4; judge reason: {s.Reason ?? "none"}");
        }
        Console.WriteLine($"  tool calls: {JsonSerializer.Serialize(o.Calls)}");
        Console.WriteLine($"  response: {o.Response}");
    }
    return result.Report.ExitCode;
});
