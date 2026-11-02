using System.Net.Http.Json;
using Unit02.Common;

return await CourseApp.RunAsync(args, async context =>
{
    var connection = context.Connect("foundry-local");
    if (!context.Demo)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var registry = await http.GetFromJsonAsync<ModelRegistry>(new Uri(connection.Endpoint, "models"));
        var ids = registry?.Data?.Select(model => model.Id).ToArray() ?? [];
        if (!ids.Contains(connection.Model, StringComparer.OrdinalIgnoreCase))
            throw new ConfigurationException(
                "Set FOUNDRY_LOCAL_API_MODEL to an id from GET /v1/models. "
                + "Keep FOUNDRY_LOCAL_MODEL as the CLI model/variant to download and load.");
        Json.Print(new { apiModels = ids, hardware = "Record the selected variant and device from the CLI." });
    }
    var incident = Tasks.Incidents[0];
    var result = await Inference.RunAsync(context, connection, Tasks.SummaryInstructions,
        incident.Prompt, incident.DemoAnswer, text => Tasks.CheckSummary(text, incident), "prepared-local-model");
    Inference.Print(result);
    context.Save("v04-local.json", result.Record);
    Console.WriteLine("After downloading packages, execution providers and weights, repeat without network access to verify offline operation.");
    if (result.Record.Status != "ok") throw new InvalidOperationException();
});

sealed record ModelRegistry(ModelRegistryEntry[]? Data);
sealed record ModelRegistryEntry(string Id);
