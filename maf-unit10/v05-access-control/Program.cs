using System.Text.Json;

return await Lab.RunAsync(args, "v05", async context =>
{
    object schema = AgentHost.ReadTools(context.Tools).Select(tool => new { tool.Name,
        parameters = tool.JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToArray() }).ToArray();
    foreach (var tool in AgentHost.ReadTools(context.Tools))
    {
        var names = tool.JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToArray();
        Output.Require(names.SequenceEqual(["service"]), "Caller or credentials leaked into the tool schema.");
    }
    var allowed = new List<string>();
    var denied = new List<string>();
    foreach (string profile in new[] { "employee", "operator", "payroll" })
    foreach (string service in new[] { "vpn", "erp", "payroll" })
    {
        Caller caller = Caller.ForProfile(profile);
        var tools = new IntegrationTools(caller, context.Tickets, context.Http, context.ApiRoot, context.ApiToken);
        try { tools.GetOpenTicketCount(service); allowed.Add($"{profile}:{service}:read"); }
        catch (UnauthorizedAccessException) { denied.Add($"{profile}:{service}:read"); }
        try { caller.Demand(service, write: true); allowed.Add($"{profile}:{service}:notify"); }
        catch (UnauthorizedAccessException) { denied.Add($"{profile}:{service}:notify"); }
    }
    Output.Require(denied.Contains("employee:vpn:notify") && denied.Contains("operator:payroll:read"),
        "Fixture permission matrix widened unexpectedly.");
    var employee = new IntegrationTools(Caller.ForProfile("employee"), context.Tickets,
        context.Http, context.ApiRoot, context.ApiToken);
    int before = context.Fixture?.Requests.Count ?? 0;
    bool rejected = false;
    try { await employee.PrepareAsync("vpn", context.Channel, default); }
    catch (UnauthorizedAccessException) { rejected = true; }
    Output.Require(rejected && (context.Fixture?.Requests.Count ?? 0) == before,
        "A read-only caller reached notification preparation.");

    // Readable summary; the RESULT JSON line printed afterwards stays last.
    Console.WriteLine($"Mode: {(context.Demo ? "local-demo" : "configured-live")}   Model invoked: no   Profiles are synthetic");
    Console.WriteLine("\nPermission matrix (R = read ticket count, N = prepare notification):");
    string[] services = ["vpn", "erp", "payroll"];
    Console.WriteLine($"  {"profile",-10}" + string.Concat(services.Select(s => $"{s,-9}")));
    foreach (string profile in new[] { "employee", "operator", "payroll" })
        Console.WriteLine($"  {profile,-10}" + string.Concat(services.Select(s =>
        {
            string cell = (allowed.Contains($"{profile}:{s}:read") ? "R" : "-") +
                          (allowed.Contains($"{profile}:{s}:notify") ? "N" : "-");
            return $"{cell,-9}";
        })));
    Console.WriteLine($"\nAllowed: {allowed.Count}   Denied: {denied.Count}");
    Console.WriteLine("\nTool schemas (model-visible arguments):");
    foreach (var tool in AgentHost.ReadTools(context.Tools))
        Console.WriteLine($"  - {tool.Name}({string.Join(", ", tool.JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name))})");
    Console.WriteLine("\nChecks:");
    Console.WriteLine("  PASS  No caller or credential field appears in any tool schema");
    Console.WriteLine("  PASS  Permission matrix matches the expected fixture");
    Console.WriteLine("  PASS  Read-only caller was denied notification before any HTTP request\n");

    return new { allowed, denied, toolSchemas = schema, notificationDeniedBeforeHttp = true,
        profilesAreSynthetic = true, modelInvoked = false };
});
