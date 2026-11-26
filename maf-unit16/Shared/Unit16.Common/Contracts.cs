using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Unit16.Common;

public sealed record Evidence(string Service, string Status, string Summary);
public sealed record ToolCall(string Name, string Argument, Evidence Result);
public sealed record CandidateResult(Evidence Evidence, string Mode, ToolCall[]? Calls,
    string? Release = null, string? PromptVersion = null);
public sealed record MigrationCase(string Id, string Input, string ExpectedStatus);

public static class Contract
{
    public static readonly IReadOnlyDictionary<string, Evidence> Catalog = new Dictionary<string, Evidence>
    {
        ["vpn"] = new("vpn", "degraded", "Synthetic evidence: VPN connectivity is degraded."),
        ["email"] = new("email", "healthy", "Synthetic evidence: email is healthy.")
    };
    public static readonly MigrationCase[] Cases =
    [
        new("vpn", "vpn", "degraded"), new("email", "email", "healthy"),
        new("normalization", " VPN ", "degraded"), new("unknown", "payroll", "unknown_service")
    ];
    public static string Normalize(string value)
    {
        string result = value.Trim().ToLowerInvariant();
        if (!Regex.IsMatch(result, "^[a-z][a-z0-9-]{0,39}$"))
            throw new ArgumentException("Invalid service identifier.");
        return result;
    }
    public static Evidence Unknown(string service) => new(service, "unknown_service", "No supported service was assessed.");
    public static string LegacyRecording(string service) => Contract.Catalog.TryGetValue(service, out Evidence? evidence)
        ? JsonSerializer.Serialize(new { service_name = service, is_available = evidence.Status == "healthy" })
        : "{\"error\":\"unknown_service\"}";
    public static Evidence AdaptLegacy(string json, string service)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.TryGetProperty("error", out JsonElement error) && error.GetString() == "unknown_service")
            return Unknown(service);
        if (root.GetProperty("service_name").GetString() != service)
            throw new InvalidDataException("Legacy service mismatch.");
        JsonElement available = root.GetProperty("is_available");
        if (available.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException("Legacy availability must be a JSON boolean.");
        return new(service, available.GetBoolean() ? "healthy" : "degraded", "Adapted legacy boundary recording.");
    }
    public static Evidence Validate(Evidence evidence, string service)
    {
        if (evidence.Service != service || evidence.Status is not ("healthy" or "degraded"))
            throw new InvalidDataException("Candidate service or status does not satisfy the contract.");
        if (string.IsNullOrWhiteSpace(evidence.Summary))
            throw new InvalidDataException("Candidate summary is required.");
        return evidence;
    }
}

public sealed class StatusTools
{
    public List<ToolCall> Calls { get; } = [];
    [Description("Read the current status of the requested synthetic service. Read-only; cannot repair or notify.")]
    public Evidence GetServiceStatus([Description("The exact normalized service name: vpn or email.")] string service)
    {
        LabConsole.Write("TOOL", $"GetServiceStatus(service=\"{service}\") [read-only]");
        if (!Contract.Catalog.TryGetValue(service, out Evidence? evidence))
            throw new ArgumentException("Unsupported service.");
        Calls.Add(new(nameof(GetServiceStatus), service, evidence));
        LabConsole.Data("TOOL RESULT", evidence);
        return evidence;
    }
}
