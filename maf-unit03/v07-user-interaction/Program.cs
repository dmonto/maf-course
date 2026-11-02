using Unit03.Common;

return await CourseApp.RunAsync(args, async context =>
{
    using var host = await AgentHost.CreateAsync(context);
    if (context.Args.Has("--interactive")) await Conversation.RunInteractiveAsync(host);
    else
    {
        var reports = await Conversation.RunAsync(host,
        [
            "What is the current status of payroll?",
            "What is the current status of VPN?",
            "When will it recover?",
            "/new",
            "What about that service?"
        ]);
        Console.WriteLine("Review whether ambiguity is clarified and recovery times are grounded in evidence.");
        context.Save("v07-user-interaction", reports);
    }
});
