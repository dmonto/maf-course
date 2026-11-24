using System.Text.Json;
using Unit12.Common;

return await App.RunAsync(async () =>
{
    CliOptions cli = CliOptions.Parse(args);
    App.Require(cli.Mode == "fixture", "This controlled regression demonstration requires fixture mode.");
    RunResult baseline = await EvaluationRunner.RunAsync(new("v03", "fixture", "baseline"));
    RunResult candidate = await EvaluationRunner.RunAsync(new("v03", "fixture-bad", "candidate"));
    App.Require(baseline.Report.ExitCode == 0 && candidate.Report.ExitCode == 1, "The regression gate did not detect the known fault.");
    Outcome fault = candidate.Report.Outcomes.Single(x => x.CaseId == "vpn-status" && x.Iteration == 1);
    App.Require(!fault.Checks["status"] && fault.Checks["toolTrace"], "The failure must come from status, not tool evidence.");
    Console.WriteLine(JsonSerializer.Serialize(Comparisons.Pair(baseline.Report, candidate.Report), Cases.Json));
    Console.WriteLine("RESULT: regression detected; positive exit=0; negative exit=1.");
    return 0;
});
