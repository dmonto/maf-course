using System.Text.Json;

return await Lab.RunAsync(args, "v09", async context =>
{
    if (context.Options.Get("--case", "walkthrough") == "acceptance")
        return await Acceptance.RunAsync(context);
    object? inspection = context.Options.Has("--agent") ? await AgentHost.InspectAsync(context) : null;
    // Readable summary; the RESULT JSON line printed afterwards stays last.
    Console.WriteLine($"Mode: {(context.Demo ? "local-demo" : "configured-live")}   Service: {context.Service}   Caller tenant: {context.Caller.Tenant}");
    if (inspection is not null)
    {
        var json = JsonSerializer.SerializeToElement(inspection, Output.Json);
        Console.WriteLine("\nAgent inspection:");
        Console.WriteLine($"  Model invoked: {(json.TryGetProperty("modelInvoked", out var mi) ? mi.ToString() : "n/a")}");
        if (json.TryGetProperty("status", out var st))
            Console.WriteLine($"  Status: {st.GetProperty("service")} is {st.GetProperty("status")} ({st.GetProperty("summary")})");
        if (json.TryGetProperty("openTickets", out var tk)) Console.WriteLine($"  Open tickets: {tk}");
        if (json.TryGetProperty("answer", out var an))
            foreach (string line in (an.GetString() ?? "").Split('\n')) Console.WriteLine("  " + line);
    }
    if (!context.Options.Has("--send"))
    {
        var preview = await context.PrepareAsync(default);
        Console.WriteLine("\nState: prepared (nothing was sent; pass --send to deliver)");
        Console.WriteLine($"Channel: {preview.Channel}\nMessage: {preview.Text}\n");
        return new { state = "prepared", preview, inspection, deliveryAttempted = false };
    }
    var result = await NotificationFlow.ExecuteAsync(context, context.Options.Get("--operation", "end-to-end-001"));
    Console.WriteLine($"\nDelivery: {result.State}   Operation: {result.OperationId}   Attempted: {(result.DeliveryAttempted ? "yes" : "no")}");
    Console.WriteLine($"Channel: {result.Preview.Channel}\nMessage: {result.Preview.Text}");
    if (result.Receipt is not null) Console.WriteLine($"Receipt: {result.Receipt}");
    if (result.Error is not null) Console.WriteLine($"Error: {result.Error}");
    Console.WriteLine();
    Output.Result(result);
    if (result.State is "rejected" or "unknown") throw new DeliveryException(result.Error ?? "Delivery failed.", result.State);
    return new { delivery = result, inspection };
});

internal static class Acceptance
{
    public static async Task<object> RunAsync(LabContext parent)
    {
        Output.Require(parent.Demo, "Acceptance checks use the local dependency fixture.");
        using var context = new LabContext(parent.Root, "v09", new Options(["--demo"]), "acceptance-" + Guid.NewGuid().ToString("N"));
        var passed = new List<string>();
        void Check(bool condition, string label) { Output.Require(condition, label); passed.Add(label); }
        var draft = await context.PrepareAsync(default);
        Check(draft.Text.Contains("vpn: degraded") && draft.Text.Contains("Open tickets: 2"), "normal-preview");
        context.Fixture!.Case = "tenant";
        bool denied = false;
        try { await context.PrepareAsync(default); } catch (InvalidDataException) { denied = true; }
        Check(denied, "cross-tenant-evidence-rejected");
        context.Fixture.Case = "normal";
        var employee = new IntegrationTools(Caller.ForProfile("employee"), context.Tickets,
            context.Http, context.ApiRoot, context.ApiToken);
        denied = false;
        try { await employee.PrepareAsync("vpn", context.Channel, default); } catch (UnauthorizedAccessException) { denied = true; }
        Check(denied, "read-only-notification-denied");
        var cancelled = await NotificationFlow.ExecuteAsync(context, "acceptance-cancel", review: (_, _) => Task.FromResult(false));
        Check(cancelled.State == "cancelled" && !cancelled.DeliveryAttempted &&
            context.Tickets.State(NotificationFlow.Key(context.Caller, "acceptance-cancel")) == "absent", "cancel-without-claim");
        context.Fixture.Case = "scope";
        var rejected = await NotificationFlow.ExecuteAsync(context, "acceptance-scope", review: (_, _) => Task.FromResult(true));
        Check(rejected.State == "rejected" && rejected.Receipt is null, "http-200-method-failure");
        context.Fixture.Case = "normal";
        var sent = await NotificationFlow.ExecuteAsync(context, "acceptance-send", review: (_, _) => Task.FromResult(true));
        Check(sent.State == "sent" && sent.Receipt is not null, "acknowledged-receipt");
        int before = context.Fixture.SuccessfulPosts;
        var duplicate = await NotificationFlow.ExecuteAsync(context, "acceptance-send", review: (_, _) => Task.FromResult(true));
        Check(duplicate.State == "already-sent" && !duplicate.DeliveryAttempted &&
            context.Fixture.SuccessfulPosts == before, "persistent-duplicate-suppression");
        string report = Path.Combine(parent.Root, "reports", "integration-acceptance.json");
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        var result = new { passed = passed.Count, checks = passed, mode = "local-demo", liveMessagesSent = 0 };
        await File.WriteAllTextAsync(report, JsonSerializer.Serialize(result, new JsonSerializerOptions(Output.Json) { WriteIndented = true }));

        // Readable summary; the RESULT JSON line printed afterwards stays last.
        Console.WriteLine($"Acceptance checks (local-demo, live messages sent: 0): {passed.Count} passed");
        foreach (string label in passed) Console.WriteLine($"  PASS  {label}");
        Console.WriteLine($"\nReport written to: {report}\n");
        return result;
    }
}
