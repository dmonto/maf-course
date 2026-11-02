using Unit03.Common;

return await CourseApp.RunAsync(args, async context =>
{
    using var host = await AgentHost.CreateAsync(context);
    var report = await host.RunTurnAsync("What is the current status of CRM?");
    Console.WriteLine($"Agent: {report.Answer}");
    bool invocationCompleted = report.Status == "completed";
    bool recordRetrieved = report.Evidence.Any(e => e.Result.Ok && e.Result.Data is not null);
    Console.WriteLine($"Invocation completed: {invocationCompleted}; authorized record retrieved: {recordRetrieved}");
    Console.WriteLine("Use structured evidence for business decisions; generated prose is display text.");
    context.Save("v06-result-management", new { invocationCompleted, recordRetrieved, report });
    if (!invocationCompleted) throw new InvalidOperationException("The invocation failed; inspect its run ID.");
});
