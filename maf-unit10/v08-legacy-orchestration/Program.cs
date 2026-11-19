return await Lab.RunAsync(args, "v08", async context =>
{
    var adapter = new LegacyStatusAdapter(context.Caller, context.Http, context.ApiRoot, context.ApiToken);
    using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    ServiceStatus status = await adapter.ReadAsync(context.Service, budget.Token);
    int count = context.Tools.GetOpenTicketCount(context.Service);
    // Translate the legacy result into the same bounded operational message contract.
    var preview = new Notification(context.Channel,
        $"TRAINING | {status.Service}: {status.Status} | Open tickets: {count} | Observed UTC: {status.ObservedAtUtc:O}",
        DateTimeOffset.UtcNow);

    // Readable summary; the RESULT JSON line printed afterwards stays last.
    string legacyCode = LegacyStatusAdapter.Code(context.Service);
    Console.WriteLine($"Mode: {(context.Demo ? "local-demo" : "configured-live")}   Service: {context.Service}   Caller tenant: {context.Caller.Tenant}");
    Console.WriteLine($"\nLegacy record: code {legacyCode}");
    Console.WriteLine($"Normalized status: {status.Service} ({status.Tenant}) is {status.Status}");
    Console.WriteLine($"  Observed: {status.ObservedAtUtc:u}");
    Console.WriteLine($"  Summary:  {status.Summary}");
    Console.WriteLine($"Open tickets: {count}");
    Console.WriteLine($"\nNotification preview ({preview.Channel}):\n  {preview.Text}");

    if (context.Options.Has("--send"))
    {
        var result = await NotificationFlow.ExecuteAsync(context, context.Options.Get("--operation", "legacy-demo-001"),
            prepare: _ => Task.FromResult(preview));
        Console.WriteLine($"\nDelivery: {result.State}   Operation: {result.OperationId}   Attempted: {(result.DeliveryAttempted ? "yes" : "no")}");
        if (result.Receipt is not null) Console.WriteLine($"  Receipt: {result.Receipt}");
        if (result.Error is not null) Console.WriteLine($"  Error: {result.Error}");
        Console.WriteLine();
        Output.Result(result);
        if (result.State is "rejected" or "unknown") throw new DeliveryException(result.Error ?? "Delivery failed.", result.State);
        return result;
    }
    Console.WriteLine("\nDelivery: not attempted (pass --send to deliver)");
    Console.WriteLine("Note: maintenance freeze is not implemented in this example.\n");
    return new { legacyCode, normalized = status,
        openTickets = count, preview, deliveryAttempted = false, maintenanceFreezeImplemented = false };
});
