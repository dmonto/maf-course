using System.Text.Json;
using Microsoft.Extensions.AI;

return await Lab.RunAsync(args, "v01", async context =>
{
    // The same function wrappers run explicitly in demo mode and through MAF in live mode.
    object inspection = await AgentHost.InspectAsync(context);
    var toolContracts = AgentHost.ReadTools(context.Tools)
        .Select(tool => new { tool.Name, tool.Description, schema = tool.JsonSchema }).ToArray();

    // Readable summary; the RESULT JSON line printed afterwards stays last.
    // Demo and live inspections have different fields, so every lookup is optional.
    var json = JsonSerializer.SerializeToElement(inspection, Output.Json);
    string Text(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) ? v.ToString() : "n/a";
    Console.WriteLine($"Mode: {(context.Demo ? "local-demo" : "configured-live")}   " +
        $"Model invoked: {Text(json, "modelInvoked")}" +
        (json.TryGetProperty("invocation", out var inv) ? $"   Invocation: {inv}" : ""));
    if (json.TryGetProperty("status", out var status))
    {
        Console.WriteLine($"\nService status: {Text(status, "service")} (tenant {Text(status, "tenant")}) is {Text(status, "status")}");
        Console.WriteLine($"  Observed: {Text(status, "observedAtUtc")}");
        Console.WriteLine($"  Summary:  {Text(status, "summary")}");
    }
    if (json.TryGetProperty("openTickets", out var tickets)) Console.WriteLine($"Open tickets: {tickets}");
    if (json.TryGetProperty("answer", out var answer))
    {
        Console.WriteLine("\nModel answer:");
        foreach (string line in (answer.GetString() ?? "").Split('\n')) Console.WriteLine("  " + line);
    }
    Console.WriteLine($"\nRegistered tools ({toolContracts.Length}):");
    foreach (var tool in toolContracts)
    {
        var toolArgs = tool.schema.GetProperty("properties").EnumerateObject().Select(p => p.Name);
        Console.WriteLine($"  - {tool.Name}({string.Join(", ", toolArgs)})");
        Console.WriteLine($"      {tool.Description}");
    }
    Console.WriteLine();

    return new { inspection, toolContracts };
});
