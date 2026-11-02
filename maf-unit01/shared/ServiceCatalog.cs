namespace MafCourse.Unit01.Shared;

public sealed record ServiceStatus(string Service, string Status, string Detail, string Source);

/// <summary>The same synthetic service records used by the GitBook example.</summary>
public static class ServiceCatalog
{
    public static ServiceStatus Lookup(string? serviceName)
    {
        string service = (serviceName ?? string.Empty).Trim().ToLowerInvariant();
        var (status, detail) = service switch
        {
            "payroll" => ("degraded", "Batch processing is delayed. No recovery time is available."),
            "vpn" => ("healthy", "The synthetic catalog reports no VPN service incident."),
            "crm" => ("maintenance", "Scheduled maintenance is in progress. No end time is available."),
            _ => ("unknown", "The synthetic catalog has no entry for this service.")
        };
        return new ServiceStatus(service, status, detail, "synthetic-status-catalog");
    }
}
