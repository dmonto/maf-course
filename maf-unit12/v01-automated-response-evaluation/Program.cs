using Unit12.Common;

return await App.RunAsync(async () =>
{
    CliOptions cli = CliOptions.Parse(args);
    Console.WriteLine($"Dataset: {Cases.Version}; cases={Cases.All.Length}; fixtureHash={Cases.FixtureHash}");
    RunResult result = await EvaluationRunner.RunAsync(new("v01", cli.Mode));
    Reports.Print(result);
    return result.Report.ExitCode;
});
