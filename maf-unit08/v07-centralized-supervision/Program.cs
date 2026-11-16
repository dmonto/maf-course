using Unit08.Common;

return await ExampleHost.RunAsync(args, "v07", "combined", async context =>
{
    CaseOutcome outcome = await context.Supervisor.RunAsync(context.NewCase(),
        context.Arguments.RunOptions, context.CancellationToken);
    return context.Complete(outcome);
});
