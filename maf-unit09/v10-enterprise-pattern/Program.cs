return await Runner.RunAsync("v10", args, async c =>
{
    string service = c.Options.Get("--service", "vpn");
    if (service is not ("vpn" or "payroll")) throw new ArgumentException("Use vpn or payroll.");
    string profile = c.Options.Get("--profile", "employee");
    // This status catalog is synthetic in both execution modes; only knowledge uses Azure.
    string status = service == "vpn" ? "healthy" : "degraded";
    var knowledge = await c.AnswerAsync(profile, $"What guidance applies to {service}?");
    Output.Result(new { runId = Guid.NewGuid().ToString("N"), executionMode = c.ExecutionMode,
        service, status, statusSource = "synthetic-status-catalog", catalogVersion = c.Settings.CatalogVersion,
        knowledge, requiresHumanReview = status == "degraded", externalActionExecuted = false,
        nextStep = status == "degraded" ? "Prepare a proposal for an authorized reviewer." : "Inspect authorized troubleshooting guidance." });
});
