using System.ComponentModel;

internal sealed record ActorContext(string UserId, IReadOnlySet<string> AllowedServices);
internal sealed record EvidenceData(string Service, string? Status,
    string? RecoveryTime, string? NextStep, string Detail, string Source);
internal sealed record ToolResult(bool Ok, string Code, EvidenceData? Data = null);
internal sealed record ToolEvent(string Tool, string Service, int Attempt, ToolResult Result);

[Description("Evidence-based service-desk answer; no business action is executed.")]
public sealed class SupportAnswer
{
    public string Outcome { get; set; } = "";
    public string? Service { get; set; }
    public string? Status { get; set; }
    public string? RecoveryTime { get; set; }
    public string? NextStep { get; set; }
    public string[] Sources { get; set; } = [];
    public bool ActionExecuted { get; set; }
    public string Summary { get; set; } = "";
}

internal static class AnswerValidator
{
    public static string[] Check(SupportAnswer answer, IReadOnlyList<ToolEvent> evidence)
    {
        var errors = new List<string>();
        if (answer.Outcome is not ("answered" or "clarification" or "unavailable" or "out-of-scope"))
            errors.Add("invalid-outcome");
        if (string.IsNullOrWhiteSpace(answer.Summary) || answer.Summary.Length > 600)
            errors.Add("invalid-summary");
        if (answer.ActionExecuted) errors.Add("unsupported-action-claim");
        if (answer.RecoveryTime is not null) errors.Add("unsupported-recovery-time");
        var rows = evidence.Where(e => e.Result.Ok && e.Result.Data is not null)
            .Select(e => e.Result.Data!).ToArray();
        if (answer.Outcome == "answered")
        {
            if (rows.Length == 0 || rows.Any(r => r.Service != answer.Service))
                errors.Add("missing-or-mixed-service-evidence");
            var status = rows.FirstOrDefault(r => r.Status is not null);
            var guidance = rows.FirstOrDefault(r => r.NextStep is not null);
            if (answer.Status != status?.Status) errors.Add("status-evidence-mismatch");
            if (answer.NextStep != guidance?.NextStep) errors.Add("guidance-evidence-mismatch");
            string[] expected = rows.Select(r => r.Source).Distinct().Order().ToArray();
            if (answer.Sources is null || !answer.Sources.Distinct().Order().SequenceEqual(expected))
                errors.Add("source-evidence-mismatch");
        }
        else
        {
            if (answer.Status is not null || answer.NextStep is not null ||
                answer.Sources is null || answer.Sources.Length != 0)
                errors.Add("unsupported-business-fields");
            if (answer.Outcome == "unavailable" &&
                (rows.Length != 0 || !evidence.Any(e => !e.Result.Ok)))
                errors.Add("missing-unavailable-evidence");
        }
        return errors.ToArray();
    }
}
