return await Lab.RunAsync(args, "v03", async context =>
{
    Console.WriteLine($"Mode: {(context.Demo ? "local-demo" : "configured-live")}   Model invoked: no");
    if (!context.Options.Has("--send"))
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var preview = await context.PrepareAsync(budget.Token);

        // Readable summary; the RESULT JSON line printed afterwards stays last.
        Console.WriteLine("State: prepared (nothing was sent; pass --send to deliver)");
        Console.WriteLine($"Channel: {preview.Channel}");
        Console.WriteLine($"Message: {preview.Text}");
        Console.WriteLine($"Prepared at: {preview.PreparedAtUtc:u}\n");
        return new { state = "prepared", preview, deliveryAttempted = false, modelInvoked = false };
    }
    string operation = context.Options.Get("--operation", "slack-demo-001");
    DeliveryOutcome result = await NotificationFlow.ExecuteAsync(context, operation);

    // Readable summary; printed before the RESULT line even when delivery failed.
    Console.WriteLine($"State: {result.State}   Operation: {result.OperationId}   Delivery attempted: {(result.DeliveryAttempted ? "yes" : "no")}");
    Console.WriteLine($"Channel: {result.Preview.Channel}");
    Console.WriteLine($"Message: {result.Preview.Text}");
    if (result.Receipt is not null) Console.WriteLine($"Receipt: {result.Receipt}");
    if (result.Error is not null) Console.WriteLine($"Error: {result.Error}");
    Console.WriteLine();

    Output.Result(result);
    if (result.State is "rejected" or "unknown")
        throw new DeliveryException(result.Error ?? "Slack delivery failed.", result.State);
    return result;
});
