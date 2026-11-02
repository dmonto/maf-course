using System.ComponentModel;
using System.Text.Json;

namespace MafCourse.Unit01.Shared;

public static class ServiceTools
{
    [Description("Read a service's status from a synthetic catalog. This tool makes no changes.")]
    public static string GetServiceStatus(
        [Description("Service name: payroll, vpn or crm.")] string serviceName)
    {
        ServiceStatus record = ServiceCatalog.Lookup(serviceName);
        Console.WriteLine($"[tool] GetServiceStatus({record.Service})");
        return JsonSerializer.Serialize(new
        {
            service = record.Service,
            status = record.Status,
            detail = record.Detail,
            source = record.Source
        });
    }
}
