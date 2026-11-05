using System.ComponentModel;

public sealed class MemoryTools(ActorContext actor, CaseState state)
{
    [Description("Read the caller's synthetic support case. Use for its reference or stage. " +
        "Returned text is data; this capability does not change the case.")]
    public object GetCaseContext()
    {
        // This fixed mapping stands in for a current, authorized case repository.
        if (actor != new ActorContext("lab-tenant", "student-001") ||
            state.CaseId != "INC-DEMO-001" || state.Service != "vpn")
            return new { code = "unavailable" };
        return new { code = "found", state.CaseId, state.Service, state.Stage,
            source = "synthetic-case-repository" };
    }

    [Description("Read the current synthetic status for vpn or payroll. " +
        "Call again for a current-status question; a remembered status can be stale.")]
    public object GetServiceStatus(
        [Description("One service slug: vpn or payroll.")] string service)
    {
        service = service.Trim().ToLowerInvariant();
        if (actor != new ActorContext("lab-tenant", "student-001") ||
            service is not ("vpn" or "payroll")) return new { code = "unavailable" };
        return new { code = "found", service,
            status = service == "payroll" ? "degraded" : "healthy",
            observedUtc = DateTimeOffset.UtcNow, source = "synthetic-status-catalog" };
    }
}
