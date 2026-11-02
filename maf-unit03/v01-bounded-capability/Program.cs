using Unit03.Common;

return await CourseApp.RunAsync(args, async context =>
{
    Console.WriteLine("Capability: explain permitted service status using catalog evidence.");
    Console.WriteLine("Completion: an evidence-based answer, clarification or controlled refusal.");
    Console.WriteLine("Available action: read status. Caller identity is supplied by the host.");
    using var host = await AgentHost.CreateAsync(context);
    var inputs = context.Args.Get("--input") is { } input ? new[] { input }
        : new[] { "What is the current status of payroll?", "Restart payroll now." };
    var reports = await Conversation.RunAsync(host, inputs);
    context.Save("v01-bounded-capability", reports);
});
