using Unit12.Common;

return await App.RunAsync(async () =>
{
    CliOptions cli = CliOptions.Parse(args);
    App.Require(cli.Mode == "fixture", "The local CI gate requires fixture mode.");
    RunResult positive = await EvaluationRunner.RunAsync(new("v08", "fixture", "positive"));
    RunResult negative = await EvaluationRunner.RunAsync(new("v08", "fixture-bad", "negative"));
    App.Require(positive.Report.ExitCode == 0 && negative.Report.ExitCode == 1,
        "CI gate failed: the positive must pass and the known negative must be rejected.");
    App.Require(Directory.GetFiles(positive.Directory, "run-*.json").Length > 0 &&
        Directory.GetFiles(negative.Directory, "run-*.json").Length > 0, "Both runs must retain their evidence.");
    Console.WriteLine("RESULT: local CI gate passed. Run Scripts/Test-FixtureGate.ps1 to verify actual child-process exit codes.");
    return 0;
});
