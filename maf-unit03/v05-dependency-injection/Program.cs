using Unit03.Common;

return await CourseApp.RunAsync(args, async context =>
{
    string scenario = context.Args.Get("--case") ?? "catalog";
    IStatusRepository repository = scenario switch
    {
        "catalog" => new StatusRepository(),
        "alternate" => new AlternateStatusRepository(),
        _ => throw new ConfigurationException("Use --case catalog or --case alternate.")
    };
    // Swap the injected collaborator; agent construction and tool registration do not change.
    using var host = await AgentHost.CreateAsync(context, repository: repository);
    if (context.Args.Has("--interactive")) await Conversation.RunInteractiveAsync(host);
    else
    {
        var reports = await Conversation.RunAsync(host,
        [
            "What is the current status of payroll?",
            "Is a recovery time available for that service?",
            "What is the current status of CRM?",
            "What is the current status of billing?",
            "Restart payroll now."
        ]);
        context.Save("v05-dependency-injection", new { scenario, reports });
    }
});

sealed class AlternateStatusRepository : IStatusRepository
{
    readonly StatusRepository fallback = new();
    public ServiceStatus? Find(string service) => service == "payroll"
        ? new(service, "healthy", "Alternate synthetic scenario: the queue is clear.", "alternate-synthetic-catalog")
        : fallback.Find(service);
}
