using Unit08.Common;

return await ExampleHost.RunAsync(args, "v05", "vpn", async context =>
{
    Delegation[] plan = Supervisor.Plan(context.NewCase());
    var checks = new List<object>();
    foreach (var task in plan)
    {
        Report report = await context.Supervisor.ConsultAsync(task,
            context.Arguments.RunOptions, context.CancellationToken);
        if (report.Outcome != "accepted") return context.Result("needs-review", report, 2);
        string rejected;
        try
        {
            Contract.Validate(task, report.Advice! with { Action = "reset-credentials" });
            throw new Exception("A disallowed recommendation was accepted.");
        }
        catch (InvalidOperationException ex) when (ex.Message == "unsupported-action")
            { rejected = ex.Message; }
        checks.Add(new { task.Role, Subject = task.Evidence.Subject,
            AllowedRecommendation = RolePolicy.AllowedAction(task.Role), CanExecuteWrites = false,
            RejectedRecommendation = rejected, AcceptedAdvice = report.Advice });
    }
    // This tests the C# recommendation policy. No Entra role is created or granted.
    return context.Result("role-boundaries-verified", checks);
});
