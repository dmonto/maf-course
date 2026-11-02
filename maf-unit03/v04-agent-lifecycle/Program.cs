using Unit03.Common;

return await CourseApp.RunAsync(args, async context =>
{
    using var host = await AgentHost.CreateAsync(context);
    Console.WriteLine("Configured agent -> ready session -> running turn -> completed turn.");
    Console.WriteLine("/new starts a conversation; /exit ends this console process.");
    if (context.Args.Has("--interactive")) await Conversation.RunInteractiveAsync(host);
    else
    {
        var reports = await Conversation.RunAsync(host,
            ["What is the current status of payroll?", "/new", "Is a recovery time available for that service?"]);
        context.Save("v04-agent-lifecycle", reports);
    }
});
