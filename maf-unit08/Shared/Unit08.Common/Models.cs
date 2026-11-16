namespace Unit08.Common;

public sealed record SupportCase(string RunId, string Scenario);
public sealed record Observation(string SourceId, string Subject, string Status, string Detail);
public sealed record Delegation(string RunId, string TaskId, string Role,
    string Question, Observation Evidence);
public sealed record Advice(string EvidenceId, string ObservedStatus, string Action, string Reason);
public sealed record Report(Delegation Task, string Outcome, Advice? Advice,
    string ErrorCode, long ElapsedMs);
public sealed record Brief(string Summary, string[] EvidenceIds);
public sealed record CaseOutcome(string RunId, string Stage, Report[] Reports, Brief? Brief);
public sealed record RunOptions(bool FailNetwork = false, bool Conflict = false, bool Serial = false);

public static class RolePolicy
{
    public static string AllowedAction(string role) => role switch
    {
        "network" => "collect-client-details",
        "service" => "refer-service-owner",
        _ => throw new InvalidOperationException("unknown-role")
    };
}

public static class Contract
{
    public static void Validate(Delegation task, Advice? advice)
    {
        if (advice is null) throw new InvalidOperationException("empty-advice");
        string allowedAction = RolePolicy.AllowedAction(task.Role);
        if (advice.EvidenceId != task.Evidence.SourceId
            || advice.ObservedStatus != task.Evidence.Status)
            throw new InvalidOperationException("unsupported-observation");
        if (advice.Action != allowedAction)
            throw new InvalidOperationException("unsupported-action");
        if (string.IsNullOrWhiteSpace(advice.Reason) || advice.Reason.Length > 600)
            throw new InvalidOperationException("invalid-reason");
    }

    public static void ValidateReport(Delegation expected, Report report)
    {
        // This extra binding check covers reports deserialized from a transport.
        if (report.Task != expected) throw new InvalidOperationException("assignment-mismatch");
        if (report.Outcome != "accepted" || report.ErrorCode != "")
            throw new InvalidOperationException("invalid-report");
        Validate(expected, report.Advice);
    }

    public static void ValidateBrief(Report[] reports, Brief? brief)
    {
        var expected = reports.Select(r => r.Task.Evidence.SourceId)
            .Order(StringComparer.Ordinal).ToArray();
        if (brief is null || string.IsNullOrWhiteSpace(brief.Summary) || brief.EvidenceIds is null)
            throw new InvalidOperationException("invalid-brief: " + System.Text.Json.JsonSerializer.Serialize(brief));
        if (brief.Summary.Length > 1200)
            throw new InvalidOperationException("invalid-brief: summary-too-long");
        if (!expected.SequenceEqual(brief.EvidenceIds.Order(StringComparer.Ordinal)))
            throw new InvalidOperationException("invalid-brief: evidence-ids [" +
                string.Join(",", brief.EvidenceIds) + "] expected [" + string.Join(",", expected) + "]");
    }
}
