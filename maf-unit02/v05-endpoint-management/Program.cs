using Unit02.Common;

return await CourseApp.RunAsync(args, async context =>
{
    var connection = context.Connect();
    var incident = Tasks.Incidents[0];
    string prompt = context.Args.Get("--prompt") ?? incident.Prompt;
    var result = await Inference.RunAsync(context, connection, Tasks.SummaryInstructions,
        prompt, incident.DemoAnswer, text => Tasks.CheckSummary(text, incident));
    Inference.Print(result);
    context.Save("v05-probe.json", result.Record);
    Console.WriteLine("elapsedMs covers the SDK request, including first-call authentication or model loading when applicable.");
    if (result.Record.Status != "ok") throw new InvalidOperationException();
});
