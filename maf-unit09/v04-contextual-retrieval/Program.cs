return await Runner.RunAsync("v04", args, async c =>
{
    string profile = c.Options.Get("--profile", "employee");
    string mode = c.Options.Get("--mode", "hybrid");
    string question = c.Options.Get("--question", "What is the recovery time for VPN error 809?");
    Output.Result(await c.AnswerAsync(profile, question, mode));
});
