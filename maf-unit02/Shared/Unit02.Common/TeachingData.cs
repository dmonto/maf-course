using System.Text.Json;
using System.Text.RegularExpressions;

namespace Unit02.Common;

public sealed record Incident(string Id, string Service, string Impact, string[] Keywords)
{
    public string Prompt => $"Synthetic incident: {Service}. {Impact} No recovery time is available. Summarize it for support.";
    public string DemoAnswer => $"Service: {Service}\nImpact: {Impact}\nNext step: Recovery time is unknown; request an update.";
}
public sealed record Ticket(string Id, string Text, string Label);

public static class Tasks
{
    public const string SummaryInstructions = "Summarize the supplied synthetic incident in three short lines: Service, Impact, Next step. Use only supplied facts. If information is missing, say so. Do not invent recovery times.";
    public const string ClassifierInstructions = "Classify the supplied synthetic support ticket. Return exactly one label: incident, service request, or question. Return no explanation.";
    public static readonly Incident[] Incidents = [
        new("INC-001", "Payroll", "Batch processing is delayed.", ["batch", "delay"]),
        new("INC-002", "Billing", "Invoice generation is unavailable.", ["invoice", "unavailable"]),
        new("INC-003", "Inventory", "Stock synchronization is delayed.", ["stock", "delay"])
    ];
    public static readonly Ticket[] Tickets = [
        new("T-001", "The payroll batch stopped unexpectedly.", "incident"),
        new("T-002", "Please create a new mailbox for a new employee.", "service request"),
        new("T-003", "Where can I read the support opening hours?", "question")
    ];
    public static QualityResult CheckSummary(string text, Incident incident)
    {
        bool service = text.Contains(incident.Service, StringComparison.OrdinalIgnoreCase);
        bool impact = incident.Keywords.All(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
        bool unknown = Regex.IsMatch(text,
            @"(?:recovery|restoration|resolution|eta)[^\r\n.!?]*(?:unknown|not\s+(?:been\s+)?(?:available|provided|supplied|known)|unavailable)"
            + @"|(?:unknown|unavailable)[^\r\n.!?]*(?:recovery|restoration|resolution|eta)|\bno recovery time\b", RegexOptions.IgnoreCase);
        bool inventedTime = Regex.IsMatch(text, @"\b\d{1,2}:\d{2}\b|\b\d+\s*(?:minutes?|hours?)\b", RegexOptions.IgnoreCase);
        return new(service && impact && unknown && !inventedTime,
            [$"service={service}", $"impact={impact}", $"unknownRecovery={unknown}", $"inventedRecoveryTime={inventedTime}"]);
    }
    public static QualityResult CheckLabel(string text, string expected)
    {
        string actual = text.Trim().Trim('"', '\'', '.', '`').Trim().ToLowerInvariant();
        return new(actual == expected, [$"expected={expected}", $"actual={actual}"]);
    }
}

public static class Costs
{
    public static decimal? Estimate(long? input, long? output, decimal inputRate, decimal outputRate)
    {
        if (inputRate < 0 || outputRate < 0 || input < 0 || output < 0) throw new ArgumentOutOfRangeException(nameof(input));
        return input is null || output is null ? null : input.Value / 1_000_000m * inputRate + output.Value / 1_000_000m * outputRate;
    }
}

public static class Statistics
{
    public static double Percentile(IEnumerable<long> source, double quantile)
    {
        var values = source.Order().ToArray();
        if (values.Length == 0 || quantile < 0 || quantile > 1) throw new ArgumentOutOfRangeException(nameof(quantile));
        double rank = (values.Length - 1) * quantile;
        int lo = (int)Math.Floor(rank), hi = (int)Math.Ceiling(rank);
        return values[lo] + (values[hi] - values[lo]) * (rank - lo);
    }
}

public sealed record ModelCandidate(string Name, string Provider, bool Offline, bool ToolCalling, int ContextTokens);
public static class Selection
{
    public static ModelCandidate[] Eligible(IEnumerable<ModelCandidate> candidates, bool offline, bool tools, int context)
        => candidates.Where(c => (!offline || c.Offline) && (!tools || c.ToolCalling) && c.ContextTokens >= context).ToArray();
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public static string Line(object value) => JsonSerializer.Serialize(value, new JsonSerializerOptions(Options) { WriteIndented = false });
    public static void Print(object value) => Console.WriteLine(JsonSerializer.Serialize(value, Options));
    public static void Save(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, Options) + Environment.NewLine);
        Console.WriteLine($"Saved: {Path.GetFileName(path)}");
    }
}
