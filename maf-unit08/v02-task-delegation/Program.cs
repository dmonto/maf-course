using Unit08.Common;

return await ExampleHost.RunAsync(args, "v02", "combined", async context =>
{
    Delegation[] tasks = Supervisor.Plan(context.NewCase());
    var reports = new List<Report>();
    // Each consultation receives one envelope, not the entire team transcript.
    foreach (var task in tasks)
        reports.Add(await context.Supervisor.ConsultAsync(task,
            context.Arguments.RunOptions, context.CancellationToken));
    bool accepted = reports.All(r => r.Outcome == "accepted");
    return context.Result(accepted ? "delegation-complete" : "needs-review",
        new { Assignments = tasks, Reports = reports, CoordinatorInvocations = 0,
            MaximumPlannedSpecialists = 2 }, accepted ? 0 : 2);
});
