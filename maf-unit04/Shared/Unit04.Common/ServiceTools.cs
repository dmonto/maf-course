using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
namespace Unit04.Common;
using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;

public sealed record ServiceStatus(
    string Service, string Status, string Detail, string Source);
public sealed record LookupResult(
    bool Ok, string Code, ServiceStatus? Data = null);

public sealed class ServiceTools(
    IHttpContextAccessor accessor, ILogger<ServiceTools> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    // Trusted synthetic caller scope, fixed by this local application.
    private static readonly HashSet<string> Allowed =
        new(["payroll", "vpn", "billing"], StringComparer.Ordinal);

    [Description("Read current status from a synthetic catalog. Returns ok, code and optional data. Read-only; never changes a service.")]
    public string GetServiceStatus(
        [Description("One service name, such as payroll or vpn.")] string serviceName)
    {
        string key = (serviceName ?? "").Trim().ToLowerInvariant();
        LookupResult result;
        if (!Regex.IsMatch(key, "^[a-z][a-z0-9-]{0,39}$"))
            result = new(false, "invalid-input");
        else if (!Allowed.Contains(key))
            result = new(false, "not-available");
        else
            result = key switch
            {
                "payroll" => new(true, "ok", new(key, "degraded",
                    "Batch processing is delayed. No recovery time is available.",
                    "synthetic-status-catalog")),
                "vpn" => new(true, "ok", new(key, "healthy",
                    "No service incident is reported in this catalog.",
                    "synthetic-status-catalog")),
                _ => new(false, "not-available")
            };

        if (accessor.HttpContext?.Items["Evidence"] is List<LookupResult> evidence)
            evidence.Add(result);
        logger.LogInformation(
            "ToolCompleted Tool={Tool} Code={Code} Ok={Ok}",
            nameof(GetServiceStatus), result.Code, result.Ok);
        return JsonSerializer.Serialize(result, JsonOptions);
    }
}
