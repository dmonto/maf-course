namespace Maf.Unit15.Domain;

public static class AssessmentPolicy
{
    public static string Decide(string status) => status switch
    {
        "degraded" => "review_required",
        "healthy" => "no_action",
        _ => throw new ArgumentException("Unsupported evidence status.")
    };
}
