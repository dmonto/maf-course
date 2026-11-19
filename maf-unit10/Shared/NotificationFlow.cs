using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

internal sealed record DeliveryOutcome(string State, string OperationId, Notification Preview,
    bool DeliveryAttempted, string? Receipt, string? Error);

internal static class NotificationFlow
{
    public static string Key(Caller caller, string operation) => $"{caller.Tenant}/{caller.Subject}/{operation}";
    public static async Task<DeliveryOutcome> ExecuteAsync(LabContext context, string operation,
        Func<CancellationToken, Task<Notification>>? prepare = null,
        Func<Notification, string, Task<bool>>? review = null)
    {
        if (!Regex.IsMatch(operation, "^[a-zA-Z0-9-]{3,40}$"))
            throw new ArgumentException("Provide a stable operation ID of 3-40 letters, digits or hyphens.");
        context.Caller.Demand(context.Service, write: true);
        Notification item;
        using (var budget = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            item = await (prepare ?? context.PrepareAsync)(budget.Token);
        if (!await (review ?? context.ReviewAsync)(item, operation))
            return new("cancelled", operation, item, false, null, null);
        context.Caller.Demand(context.Service, write: true);
        if (DateTimeOffset.UtcNow - item.PreparedAtUtc > TimeSpan.FromMinutes(2))
            throw new InvalidOperationException("Preview expired. Prepare and review fresh evidence.");
        string key = Key(context.Caller, operation);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            context.SlackEndpoint + "\n" + item.Channel + "\n" + item.Text)));
        // Resolve configuration before claiming the operation, as in the page's host.
        SlackSender sender = context.Sender();
        if (!context.Tickets.TryClaim(key, hash))
            return new("already-" + context.Tickets.State(key), operation, item, false, null, null);
        try
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            string receipt = await sender.SendAsync(item, budget.Token);
            context.Tickets.Complete(key, "sent", receipt);
            return new("sent", operation, item, true, receipt, null);
        }
        catch (Exception ex)
        {
            string state = ex is DeliveryException delivery ? delivery.State : "unknown";
            context.Tickets.Complete(key, state, null);
            return new(state, operation, item, true, null, ex.Message);
        }
    }
}
