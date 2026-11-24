using System.Text.Json;
using Unit12.Common;

return await App.RunAsync(async () =>
{
    CliOptions cli = CliOptions.Parse(args);
    RunResult result = await EvaluationRunner.RunAsync(new("v05", cli.Mode));
    foreach (Outcome o in result.Report.Outcomes)
        Console.WriteLine(JsonSerializer.Serialize(new { o.CaseId, o.Iteration, o.Calls, o.Checks }, Cases.Json));
    Console.WriteLine("The tool trace comes from application execution, not from the assistant's claims.");
    return result.Report.ExitCode;
});
