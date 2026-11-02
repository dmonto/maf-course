using Unit03.Common;

return await CourseApp.RunAsync(args, async context =>
{
    using var host = await AgentHost.CreateAsync(context);
    var reports = await Conversation.RunAsync(host,
    [
        "What is the current status of payroll?",
        "Is a recovery time available for that service?",
        "/new",
        "What about that service?"
    ]);
    Console.WriteLine("Compare conversation IDs: the first two turns share a session; the last starts fresh.");
    context.Save("v03-conversational-context", reports);
});
