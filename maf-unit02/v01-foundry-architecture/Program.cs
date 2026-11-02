using Unit02.Common;

return await CourseApp.RunAsync(args, async context =>
{
    // This function runs in this application process, not inside the model service.
    Incident ReadIncident(string id) => Tasks.Incidents.Single(i => i.Id == id);
    var incident = ReadIncident("INC-001");
    var connection = context.Connect();
    Json.Print(new {
        applicationBoundary = "local .NET process (or its deployed application host)",
        applicationProcessId = Environment.ProcessId,
        functionBoundary = "ReadIncident: in-process synthetic incident store",
        inferenceBoundary = connection.Provider == "foundry-local" ? "on-device runtime" : "Azure model service",
        projectBoundary = "development assets and access; no server-managed agent is created",
        connection.Provider, connection.Model, connection.Demo
    });
    var result = await Inference.RunAsync(context, connection, Tasks.SummaryInstructions,
        incident.Prompt, incident.DemoAnswer, text => Tasks.CheckSummary(text, incident));
    Inference.Print(result);
    context.Save("v01-boundaries.json", result.Record);
    if (result.Record.Status != "ok") throw new InvalidOperationException();
});
