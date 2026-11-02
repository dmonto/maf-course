using System.Collections.Frozen;
using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Unit03.Common;

public sealed record ActorContext(string UserId, IReadOnlySet<string> AllowedServices)
{
    public static ActorContext Student => new("student-001",
        new[] { "payroll", "vpn", "billing" }.ToFrozenSet(StringComparer.Ordinal));
}
public sealed record ServiceStatus(string Service, string Status, string Detail, string Source);
public sealed record ToolResult(bool Ok, string Code, object? Data = null);
public sealed record ToolEvidence(string Tool, int Attempt, ToolResult Result);

public interface IStatusRepository
{
    ServiceStatus? Find(string service);
}

public sealed class StatusRepository : IStatusRepository
{
    public int Reads { get; private set; }
    public ServiceStatus? Find(string service)
    {
        Reads++;
        return service switch
        {
            "payroll" => new(service, "degraded", "Batch processing is delayed. No recovery time is available.", "synthetic-status-catalog"),
            "vpn" => new(service, "healthy", "No service incident is reported in this catalog.", "synthetic-status-catalog"),
            "crm" => new(service, "maintenance", "Scheduled maintenance is in progress.", "synthetic-status-catalog"),
            _ => null
        };
    }
}

public sealed class Audit(TextWriter writer)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public void Write(string eventName, object data) =>
        writer.WriteLine(JsonSerializer.Serialize(new { eventName, data }, JsonOptions));
}

// One runtime is shared by all tools of one caller and one sequential turn.
public sealed class ToolRuntime(ActorContext actor, int maximumAttempts, Audit audit)
{
    public ActorContext Actor => actor;
    public string RunId { get; private set; } = "";
    public int Attempts { get; private set; }
    public List<ToolEvidence> Evidence { get; } = [];
    public void BeginTurn(string runId)
    {
        RunId = runId;
        Attempts = 0;
        Evidence.Clear();
    }
    public ToolResult Read(string tool, string serviceName, Func<string, object?> lookup,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Attempts++;
        string service = (serviceName ?? "").Trim().ToLowerInvariant();
        ToolResult result;
        if (Attempts > maximumAttempts) result = new(false, "tool-budget-exceeded");
        else if (!Regex.IsMatch(service, "^[a-z][a-z0-9-]{0,39}$")) result = new(false, "invalid-input");
        else if (!actor.AllowedServices.Contains(service)) result = new(false, "not-available");
        else result = lookup(service) is { } data ? new(true, "ok", data) : new(false, "not-available");
        Evidence.Add(new(tool, Attempts, result));
        audit.Write("tool-end", new { runId = RunId, actor.UserId, tool, attempt = Attempts, result.Code });
        return result;
    }
}

public sealed class ServiceTools(IStatusRepository repository, ToolRuntime runtime)
{
    [Description("Read current status from a synthetic catalog. Read-only. Returns ok, code and optional data. Never changes a service.")]
    public string GetServiceStatus(
        [Description("One service name, such as payroll, vpn or billing.")] string serviceName,
        CancellationToken cancellationToken = default) => JsonSerializer.Serialize(
            runtime.Read(nameof(GetServiceStatus), serviceName, repository.Find, cancellationToken), Audit.JsonOptions);
}
