using System.Text.RegularExpressions;

internal sealed record Caller(string Tenant, string Subject, string[] Services, bool CanNotify)
{
    // Operator-selected fixtures; replace with verified identity in a shared service.
    public static Caller ForProfile(string profile) => profile switch
    {
        "employee" => new("training-a", "employee-01", ["vpn", "erp"], false),
        "operator" => new("training-a", "operator-01", ["vpn", "erp"], true),
        "payroll" => new("training-a", "payroll-01", ["payroll"], true),
        _ => throw new ArgumentException("Unknown synthetic profile.")
    };

    public void Demand(string service, bool write = false)
    {
        if (!Services.Contains(service, StringComparer.Ordinal) || (write && !CanNotify))
            throw new UnauthorizedAccessException("Operation is not permitted for this profile.");
    }
}

internal sealed record ServiceStatus(string Service, string Tenant, string Status,
    DateTimeOffset ObservedAtUtc, string Summary)
{
    public void Validate(Caller caller, string requestedService)
    {
        if (Service != requestedService || Tenant != caller.Tenant ||
            Status is not ("healthy" or "degraded" or "unavailable") ||
            Summary is null || Summary.Length > 240 ||
            ObservedAtUtc < DateTimeOffset.UtcNow.AddMinutes(-5) ||
            ObservedAtUtc > DateTimeOffset.UtcNow.AddSeconds(30))
            throw new InvalidDataException("The status response failed contract validation.");
    }
}

internal sealed record Notification(string Channel, string Text, DateTimeOffset PreparedAtUtc);
internal sealed record SlackReply(bool Ok, string? Channel, string? Ts, string? Error);
internal sealed class DeliveryException(string message, string state) : Exception(message)
{
    public string State { get; } = state;
}

internal static class LabConfig
{
    public static string Required(string name)
    {
        string value = Environment.GetEnvironmentVariable(name)?.Trim() ?? "";
        if (value.Length == 0 || value.Contains('<'))
            throw new InvalidOperationException($"Load the assigned {name}.");
        return value;
    }

    public static bool LiveSlack => Environment.GetEnvironmentVariable("SLACK_MODE") switch
    {
        null or "fixture" => false,
        "live" => true,
        _ => throw new InvalidOperationException("SLACK_MODE must be fixture or live.")
    };

    public static Uri StatusRoot()
    {
        var uri = new Uri(Required("CORPORATE_API_BASE_URL"));
        bool local = uri.AbsoluteUri == "http://127.0.0.1:5098/";
        if ((!local && uri.Scheme != "https") || uri.UserInfo.Length != 0 ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0 || !uri.AbsolutePath.EndsWith('/'))
            throw new InvalidOperationException("Use the assigned HTTPS API root or the local fixture.");
        return uri;
    }

    public static string Channel()
    {
        string value = LiveSlack ? Required("SLACK_CHANNEL_ID") : "C000TRAINING";
        if (!Regex.IsMatch(value, "^C[A-Z0-9]{6,}$"))
            throw new InvalidOperationException("Use the assigned training channel ID.");
        return value;
    }
}
