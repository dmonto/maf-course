using Unit08.Common;

return await ExampleHost.RunAsync(args, "v03", "payroll", async context =>
{
    if (context.Arguments.Has("--fail-network"))
        throw new ArgumentException("v03 demonstrates --conflict; use v08 for --fail-network.");
    SupportCase input = context.NewCase();
    // Change the generated observation field; preserve the original catalog facts.
    var options = context.Arguments.RunOptions with { Conflict = true };
    CaseOutcome outcome = await context.Supervisor.RunAsync(input, options, context.CancellationToken);
    if (outcome.Stage != "needs-review" || outcome.Brief is not null
        || outcome.Reports.Any(r => r.ErrorCode != "unsupported-observation")
        || context.Supervisor.Metrics.SynthesisInvocations != 0)
        throw new InvalidOperationException("conflict-was-not-contained");
    return context.Complete(outcome);
});
