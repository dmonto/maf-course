using Unit12.Common;

return await App.RunAsync(async () =>
{
    CliOptions cli = CliOptions.Parse(args);
    RunResult result = await EvaluationRunner.RunAsync(new("v07", cli.Mode));
    Reports.Print(result);
    return result.Report.ExitCode;
});
