using Unit08.Common;

return await ExampleHost.RunAsync(args, "v10", "combined", async context =>
{
    if (context.Arguments.Has("--fail-network") || context.Arguments.Has("--conflict"))
        throw new ArgumentException("v10 supplies its own failure cases.");
    var checks = new[]
    {
        (Scenario: "vpn", Options: new RunOptions(), Stage: "information-ready", Reports: 1),
        (Scenario: "payroll", Options: new RunOptions(), Stage: "escalation-proposal", Reports: 1),
        (Scenario: "combined", Options: new RunOptions(), Stage: "escalation-proposal", Reports: 2),
        (Scenario: "combined", Options: new RunOptions(FailNetwork: true), Stage: "needs-review", Reports: 2),
        (Scenario: "payroll", Options: new RunOptions(Conflict: true), Stage: "needs-review", Reports: 1)
    };
    var observations = new List<object>();
    foreach (var check in checks)
    {
        SupportCase input = context.NewCase(check.Scenario);
        CaseOutcome outcome = await context.Supervisor.RunAsync(input, check.Options, context.CancellationToken);
        bool accepted = outcome.Stage == check.Stage && outcome.Reports.Length == check.Reports
            && (check.Stage != "needs-review" || outcome.Brief is null);
        var events = context.Audit.Events.Where(e => e.RunId == input.RunId).ToArray();
        if (check.Stage == "needs-review") accepted &= events.All(e => e.Event != "synthesis-start");
        else
        {
            int synthesis = Array.FindIndex(events, e => e.Event == "synthesis-start");
            int lastAcceptance = Array.FindLastIndex(events, e => e.Event == "specialist-accepted");
            accepted &= synthesis > lastAcceptance && lastAcceptance >= 0;
        }
        if (!accepted) throw new InvalidOperationException("coordination-check-failed");
        observations.Add(new { check.Scenario, check.Options, Passed = true, Outcome = outcome });
    }
    return context.Result("checks-passed", new { Checks = observations,
        LiveLanguageStillRequiresSemanticReview = context.Settings.Live });
});
