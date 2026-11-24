using Unit13;
using System.Text.Json;
using System.Security.Claims;

return await Entry.RunAsync("v07", args, context =>
{
    context.FixtureOnly();
    MemoryAuditSink audit = new();
    SecurityTools bounded = new(() => Fixtures.Operator, "vpn", Guid.NewGuid().ToString("N"), audit);
    Lab.Require(bounded.GetServiceStatus("payroll").Status == "forbidden", "Tool escaped the selected request.");
    Lab.Require(audit.Events.Single().Reason == "outside_request", "Missing risk evidence.");
    SecurityTools unavailable = new(() => Fixtures.Reader, "vpn", Guid.NewGuid().ToString("N"), new UnavailableAudit());
    bool outageWithheld = false;
    try { unavailable.GetServiceStatus("vpn"); } catch (IOException) { outageWithheld = unavailable.Returned.Count == 0; }
    Lab.Require(outageWithheld, "Audit outage released information.");
    Decision crossTenant = Authorization.Evaluate(Fixtures.Reader with { Tenant = "tenant-b" }, "vpn", "vpn", Fixtures.Rows());
    Lab.Require(!crossTenant.Allowed, "Tenant boundary failed.");
    context.Save("risk-evidence.json", new
    {
        controls = new[] { "request binding", "fail-closed audit", "tenant boundary" },
        unexpectedToolSelectionDenied = true, auditOutageWithheld = outageWithheld, tenantDenied = !crossTenant.Allowed,
        remainingTests = new[] { "hosted token validation", "adversarial model behavior", "network configuration", "statutory assessment" }
    });
    Lab.Show(new { negativeControlsPassed = 3 });
    return Task.FromResult(0);
});

sealed class UnavailableAudit : IAuditSink
{
    public void Write(AuditEvent item) => throw new IOException("Synthetic audit outage.");
}
