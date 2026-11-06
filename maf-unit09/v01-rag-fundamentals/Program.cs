return await Runner.RunAsync("v01", args, async c =>
{
    string question = c.Options.Get("--question", "What should I collect for Windows 11 VPN error 809?");
    Output.Result(await c.AnswerAsync(c.Options.Get("--profile", "employee"), question));
});
