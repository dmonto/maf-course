return await Lab.RunAsync(args, "v06", async context =>
{
    if (!context.Demo)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var liveStatus = await context.Tools.GetLiveStatusAsync(context.Service, budget.Token);

        // Readable summary; the RESULT JSON line printed afterwards stays last.
        Console.WriteLine("Mode: configured-live   Fault injected: no   Model invoked: no");
        Console.WriteLine($"Validated live status for '{context.Service}':");
        Console.WriteLine("  " + System.Text.Json.JsonSerializer.Serialize(liveStatus, Output.Json));
        Console.WriteLine();
        return new { liveStatus, faultInjected = false, modelInvoked = false };
    }
    string[] cases = ["stale", "future", "invalid", "tenant", "service", "missing", "malformed", "oversized", "media"];
    string requested = context.Options.Get("--case", "all");
    Output.Require(requested == "all" || cases.Contains(requested), "Choose a documented validation case or all.");
    var checks = new List<(string Test, bool Rejected, string? ErrorType)>();
    foreach (string item in requested == "all" ? cases : [requested])
    {
        context.Fixture!.Case = item;
        bool rejected = false;
        string? error = null;
        try { await context.Tools.GetLiveStatusAsync(context.Service); }
        catch (Exception ex) { rejected = true; error = ex.GetType().Name; }
        Output.Require(rejected, $"Invalid evidence was accepted: {item}.");
        checks.Add((item, rejected, error));
    }
    context.Fixture!.Case = "injection";
    Notification draft = await context.PrepareAsync(default);
    Output.Require(!draft.Text.Contains("IGNORE") && !draft.Text.Contains("PAYROLL"),
        "External free text entered a notification.");

    // Readable summary; the RESULT JSON line printed afterwards stays last.
    Console.WriteLine($"Mode: local-demo   Case(s): {requested}   Model invoked: no   Delivery attempted: no");
    Console.WriteLine("\nMalformed external evidence must be rejected:");
    foreach (var check in checks)
        Console.WriteLine($"  REJECTED  {check.Test,-10} ({check.ErrorType})");
    Console.WriteLine("\nPrompt-injection text in external data:");
    Console.WriteLine("  PASS  Free text from the status service never entered the notification");
    Console.WriteLine($"\nNotification preview ({draft.Channel}):\n  {draft.Text}\n");

    return new { checks = checks.Select(c => new { test = c.Test, rejected = c.Rejected, errorType = c.ErrorType }),
        externalTextExcludedFromNotification = true, preview = draft, deliveryAttempted = false, modelInvoked = false };
});
