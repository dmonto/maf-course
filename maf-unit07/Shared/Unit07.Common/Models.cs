namespace Unit07.Common;

public sealed record SupportRequest(string CaseId, string Service, string Symptom);
public sealed record Evidence(SupportRequest Request, string Status, string Guide);
public sealed record Proposal(Evidence Evidence, string Draft, bool NeedsApproval);
public sealed record Outcome(string CaseId, string Stage, string Status, string Draft);
public static class LabState
{
    public const string Scope = "support-v1";
    public const string ProposalKey = "proposal";
}
