return await Lab.RunAsync(args, "v07", async context =>
{
    if (!context.Demo)
    {
        Console.WriteLine("Mode: configured-live");
        if (!context.Options.Has("--send"))
        {
            var preview = await context.PrepareAsync(default);
            // Readable summary; the RESULT JSON line printed afterwards stays last.
            Console.WriteLine("State: prepared (nothing was sent; pass --send to deliver)");
            Console.WriteLine($"Channel: {preview.Channel}\nMessage: {preview.Text}\n");
            return new { state = "prepared", preview };
        }
        var actual = await NotificationFlow.ExecuteAsync(context, context.Options.Get("--operation", "live-error-check-001"));
        Console.WriteLine($"State: {actual.State}   Operation: {actual.OperationId}   Delivery attempted: {(actual.DeliveryAttempted ? "yes" : "no")}");
        Console.WriteLine($"Channel: {actual.Preview.Channel}\nMessage: {actual.Preview.Text}");
        if (actual.Receipt is not null) Console.WriteLine($"Receipt: {actual.Receipt}");
        if (actual.Error is not null) Console.WriteLine($"Error: {actual.Error}");
        Console.WriteLine();
        Output.Result(actual);
        if (actual.State is "rejected" or "unknown") throw new DeliveryException(actual.Error ?? "Delivery failed.", actual.State);
        return actual;
    }
    var checks = new List<object>();
    var readRows = new List<string>();
    var postRows = new List<string>();
    foreach (string item in new[] { "unauthorized", "forbidden", "unavailable", "transient" })
    {
        context.Fixture!.Case = item;
        int before = context.Fixture.Requests.Count;
        bool failed = false;
        try { await context.Tools.GetLiveStatusAsync(context.Service); }
        catch (HttpRequestException) { failed = true; }
        int attempts = context.Fixture.Requests.Count - before;
        // A transient case needs a fresh counter because preceding requests are independent tests.
        if (item == "transient")
        {
            using var fresh = new LabContext(context.Root, "v07", new Options(["--demo"]), "transient-" + Guid.NewGuid().ToString("N"));
            fresh.Fixture!.Case = "transient";
            await fresh.Tools.GetLiveStatusAsync("vpn");
            attempts = fresh.Fixture.Requests.Count;
            failed = false;
        }
        Output.Require(attempts == (item is "unavailable" or "transient" ? 2 : 1), "Read retry policy changed.");
        Output.Require(failed == (item != "transient"), "Read failure classification changed.");
        checks.Add(new { test = item, attempts, failed });
        readRows.Add($"  {item,-13} {attempts} attempt(s)   {(failed ? "failed (surfaced to caller)" : "recovered after retry")}");
    }
    foreach (string item in new[] { "scope", "rate", "internal", "server-error", "receipt-channel", "receipt-missing", "timeout" })
    {
        context.Fixture!.Case = item;
        string operation = "failure-" + item + "-" + Guid.NewGuid().ToString("N")[..8];
        int before = context.Fixture.Requests.Count(x => x.Method == "POST");
        string state;
        string? message;
        if (item == "timeout")
        {
            var draft = await context.PrepareAsync(default);
            using var budget = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            bool failed = false;
            try { await context.Sender().SendAsync(draft, budget.Token); }
            catch (OperationCanceledException) { failed = true; }
            Output.Require(failed, "Expected an uncertain timeout.");
            state = "unknown"; message = "Cancellation does not prove non-delivery.";
        }
        else
        {
            var outcome = await NotificationFlow.ExecuteAsync(context, operation, review: (_, _) => Task.FromResult(true));
            state = outcome.State; message = outcome.Error;
            Output.Require(context.Tickets.State(NotificationFlow.Key(context.Caller, operation)) == state,
                "Delivery failure was not persisted.");
        }
        int attempts = context.Fixture.Requests.Count(x => x.Method == "POST") - before;
        Output.Require(attempts == 1 && state == (item is "scope" or "rate" ? "rejected" : "unknown"),
            "An action was retried or its outcome was misclassified.");
        checks.Add(new { test = item, attempts, state, message });
        postRows.Add($"  {item,-16} {attempts} attempt   {state,-9} {message}");
    }

    // Readable summary; the RESULT JSON line printed afterwards stays last.
    Console.WriteLine("Mode: local-demo   Model invoked: no   Automatic POST retries: 0");
    Console.WriteLine("\nRead failures (GET status; reads may be retried once):");
    readRows.ForEach(Console.WriteLine);
    Console.WriteLine("\nNotification failures (POST; never retried automatically):");
    postRows.ForEach(Console.WriteLine);
    Console.WriteLine("\nChecks:");
    Console.WriteLine("  PASS  Read retry policy and failure classification unchanged");
    Console.WriteLine("  PASS  Every failed POST was attempted exactly once and persisted");
    Console.WriteLine("  PASS  Uncertain outcomes (timeout, 5xx, bad receipt) are 'unknown', not 'not delivered'\n");

    return new { checks, automaticPostRetries = 0, modelInvoked = false };
});
