using System.ComponentModel;
using Microsoft.Extensions.AI;
using Unit06.Common;

namespace Unit06.Exercise01;

public sealed record ConfirmedCaseState(string CaseId, string Service, string Stage,
    string? ReportedOperatingSystem = null, string? Provenance = null,
    DateTimeOffset? ConfirmedUtc = null) : CaseState(CaseId, Service, Stage);

public sealed class ConfirmedCaseTools(ActorContext actor, ConfirmedCaseState state, ToolTrace trace)
{
    private readonly MemoryTools _common = new(actor, state, trace);

    [Description("Read the authorized case reference, stage and user-confirmed operating system. " +
        "The operating system is user-reported information, not an infrastructure measurement.")]
    public object GetCaseContext()
    {
        trace.Add(nameof(GetCaseContext));
        if (!Lab.CanReadCase(actor, state)) return new { code = "unavailable" };
        return new { code = "found", state.CaseId, state.Service, state.Stage,
            state.ReportedOperatingSystem, state.Provenance, state.ConfirmedUtc,
            source = "synthetic-case-repository" };
    }

    public List<AITool> CreateTools() => [AIFunctionFactory.Create(GetCaseContext),
        AIFunctionFactory.Create(_common.GetServiceStatus)];
}
