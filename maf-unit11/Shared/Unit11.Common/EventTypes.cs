namespace Maf.Unit11;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

public static class LabJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
}

public sealed record StatusData(string Service, string Status, string Summary);
public sealed record SupportEvent(string Id, string Type, string Tenant,
    string CorrelationId, DateTimeOffset OccurredAtUtc, StatusData Data)
{
    public void Validate()
    {
        if (Id is null || !Regex.IsMatch(Id, "^[A-Z0-9][A-Z0-9-]{2,63}$") ||
            Type != "ServiceStatusChanged.v1" || Tenant != "training-a" ||
            !Guid.TryParse(CorrelationId, out _) ||
            OccurredAtUtc == default || OccurredAtUtc > DateTimeOffset.UtcNow.AddSeconds(30) ||
            Data is null || Data.Service is not ("vpn" or "erp") ||
            Data.Status is not ("healthy" or "degraded" or "unavailable") ||
            string.IsNullOrWhiteSpace(Data.Summary) || Data.Summary.Length > 240)
            throw new InvalidDataException("Event contract rejected.");
    }
}

public sealed record Claim(SupportEvent Event, int Attempt, string Owner, long AcceptedMs);
public sealed record JobView(string Id, string State, int Attempts, long AcceptedMs,
    string? Result, string? ErrorCode, double? FirstTextMs, double? ProcessingMs);

public static class LabConfig
{
    public static string Required(string name)
    {
        string value = Environment.GetEnvironmentVariable(name)?.Trim() ?? "";
        if (value.Length == 0 || value.Contains('<') || value.Contains("YOUR-", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Load the assigned {name}.");
        return value;
    }
    public static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
