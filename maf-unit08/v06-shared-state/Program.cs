using Unit08.Common;

return await ExampleHost.RunAsync(args, "v06", "combined", async context =>
{
    var cases = new[] { context.NewCase(), context.NewCase() };
    Supervisor supervisor = context.Supervisor;
    CaseOutcome[] outcomes = await Task.WhenAll(cases.Select(input =>
        supervisor.RunAsync(input, context.Arguments.RunOptions, context.CancellationToken)));
    for (int i = 0; i < cases.Length; i++)
    {
        if (outcomes[i].RunId != cases[i].RunId || outcomes[i].Reports.Any(r =>
            r.Task.RunId != cases[i].RunId || !r.Task.TaskId.StartsWith(cases[i].RunId + ":")))
            throw new InvalidOperationException("cross-case-contamination");
    }
    if (cases[0].RunId == cases[1].RunId) throw new InvalidOperationException("duplicate-run-id");
    bool review = outcomes.Any(o => o.Stage == "needs-review");
    return context.Result(review ? "needs-review" : "isolation-verified",
        new { Outcomes = outcomes, IsolationCheckPassed = true,
            StateOwner = "one local report collection per run" }, review ? 2 : 0);
});
