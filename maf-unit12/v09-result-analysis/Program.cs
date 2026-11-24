using System.Text.Json;
using Unit12.Common;

return await App.RunAsync(async () =>
{
    CliOptions cli = CliOptions.Parse(args, "report");
    RunReport report = cli.Get("report") is string file ? Reports.Load(file) :
        (await EvaluationRunner.RunAsync(new("v09", cli.Mode == "fixture" ? "fixture-bad" : cli.Mode))).Report;
    foreach (Outcome o in report.Outcomes.Where(x => !x.Passed))
        Console.WriteLine(JsonSerializer.Serialize(new { o.CaseId, o.Iteration, o.Error,
            failedChecks = o.Checks.Where(x => !x.Value).Select(x => x.Key), o.Scores, o.Calls, o.Response }, Cases.Json));
    string html = await Reports.WriteHtmlAsync(report, Reports.ArtifactDirectory("v09"));
    Console.WriteLine($"Evidence report: {html}; sourceExitCode={report.ExitCode}; judgeStatus={report.JudgeStatus}");
    Console.WriteLine("Use Scripts/New-QualityReport.ps1 for the separate aieval HTML after an Azure run.");
    return 0; // Analysis completed; the evaluated candidate retains its original exit code in the evidence.
});
