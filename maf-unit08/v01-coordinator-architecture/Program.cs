using Unit08.Common;

return await ExampleHost.RunAsync(args, "v01", "vpn", async context =>
{
    SupportCase input = context.NewCase();
    Delegation[] plan = Supervisor.Plan(input);
    var responsibilities = new[]
    {
        new { Component = "supervisor", Owns = "planning, validation and the business stage" },
        new { Component = "network", Owns = "the VPN observation" },
        new { Component = "service", Owns = "the payroll observation" },
        new { Component = "coordinator", Owns = "synthesis of accepted reports" }
    };
    CaseOutcome outcome = await context.Supervisor.RunAsync(input,
        context.Arguments.RunOptions, context.CancellationToken);
    return context.Result(outcome.Stage, new { Responsibilities = responsibilities, Plan = plan, Outcome = outcome },
        outcome.Stage == "needs-review" ? 2 : 0);
});
