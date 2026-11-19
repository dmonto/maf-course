using System.Globalization;
using System.Net.Http.Headers;

internal sealed record LegacyStatus(string ServiceCode, string Tenant, string Health, string ObservedUtc);

internal sealed class LegacyStatusAdapter(Caller caller, HttpClient http, Uri root, string token)
{
    public static string Code(string service) => service switch
    {
        "vpn" => "VPN01", "erp" => "ERP01", "payroll" => "PAY01",
        _ => throw new ArgumentException("Unsupported legacy service.")
    };
    public async Task<ServiceStatus> ReadAsync(string service, CancellationToken ct)
    {
        caller.Demand(service);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(root, "legacy/status/" + Uri.EscapeDataString(service)));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var legacy = await BoundedJson.ReadAsync<LegacyStatus>(response, ct);
        if (legacy.ServiceCode != Code(service) || legacy.Tenant != caller.Tenant ||
            !DateTimeOffset.TryParseExact(legacy.ObservedUtc, "O", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var observed))
            throw new InvalidDataException("Legacy service, tenant or timestamp failed validation.");
        string status = legacy.Health switch
        {
            "H" => "healthy", "D" => "degraded", "U" => "unavailable",
            _ => throw new InvalidDataException("Unknown legacy health flag.")
        };
        var result = new ServiceStatus(service, caller.Tenant, status, observed.ToUniversalTime(), "Validated legacy status adapter.");
        result.Validate(caller, service);
        return result;
    }
}
