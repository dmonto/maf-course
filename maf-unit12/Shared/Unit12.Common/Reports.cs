using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Unit12.Common;

public static class Reports
{
    public static string ArtifactDirectory(string example, string? label = null)
    {
        foreach (string name in new[] { example, label ?? "default" })
            if (!Regex.IsMatch(name, "^[a-zA-Z0-9][a-zA-Z0-9_-]*$")) throw new ArgumentException("Invalid artifact scope.");
        string root = Path.Combine(UnitEnvironment.Root, "artifacts", example);
        return label is null ? root : Path.Combine(root, label);
    }

    public static async Task<string> SaveAsync(RunReport report, string root)
    {
        Directory.CreateDirectory(root);
        string text = JsonSerializer.Serialize(report, Cases.Json);
        string history = Path.Combine(root, $"run-{report.RunId}.json");
        await File.WriteAllTextAsync(history, text);
        string temporary = Path.Combine(root, $"latest-{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(temporary, text);
        File.Move(temporary, Path.Combine(root, "latest.json"), overwrite: true);
        return history;
    }

    public static RunReport Load(string path)
    {
        var report = JsonSerializer.Deserialize<RunReport>(File.ReadAllText(Path.GetFullPath(path)), Cases.Json)
            ?? throw new InvalidOperationException("The report is empty.");
        App.Require(report.Outcomes is { Length: > 0 } && report.DatasetHash is not null && report.PromptHash is not null &&
            report.FixtureHash is not null && report.Repetitions is >= 1 and <= 5 && report.Outcomes.All(o =>
                o is not null && o.CaseId is not null && o.Checks is not null && o.Scores is not null && o.Calls is not null &&
                o.Iteration >= 1 && o.Iteration <= report.Repetitions && o.AgentMs >= 0), "The report schema is incomplete.");
        bool live = report.Mode == "azure";
        App.Require(report.Mode is "azure" or "fixture" or "fixture-bad", "Unrecognized report mode.");
        App.Require(report.JudgeStatus == (live ? "run" : "not_run") && report.Outcomes.All(o => o.JudgeRequired == live),
            "Inconsistent judge metadata.");
        App.Require(report.ExitCode == EvaluationRunner.ExitCode(report.Outcomes, live), "Inconsistent report exit code.");
        return report;
    }

    public static void Print(RunResult result)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { report = result.File, result.Report.ExitCode,
            result.Report.JudgeStatus, cases = result.Report.Outcomes.Length,
            passed = result.Report.Outcomes.Count(x => x.Passed) }, Cases.Json));
    }

    public static async Task<string> WriteHtmlAsync(RunReport report, string root)
    {
        static string H(object? x) => HtmlEncoder.Default.Encode(x?.ToString() ?? "");
        var html = new StringBuilder("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>Unit 12 evidence</title>" +
            "<style>body{font:16px system-ui;max-width:1100px;margin:40px auto;padding:0 20px;color:#182536}" +
            "table{border-collapse:collapse;width:100%}th,td{border:1px solid #ccd4de;padding:10px;text-align:left}" +
            "pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#f1f5fa;padding:14px}h1{color:#174c81}</style>");
        html.Append($"<h1>Unit 12 evaluation evidence</h1><p>Run: {H(report.RunId)} · Mode: {H(report.Mode)} · Exit: {report.ExitCode}</p>");
        html.Append(report.JudgeStatus == "not_run" ? "<p><strong>Fixture evidence only. Semantic judges were not run.</strong></p>" :
            "<p>Azure evaluation. See the separate aieval report for the reporting-library scenarios.</p>");
        html.Append("<table><tr><th>Case / iteration</th><th>Result</th><th>Failed checks</th><th>Agent ms</th><th>Judge metrics</th></tr>");
        foreach (Outcome o in report.Outcomes)
            html.Append($"<tr><td>{H(o.CaseId)}/{o.Iteration}</td><td>{(o.Passed ? "PASS" : "FAIL")}</td>" +
                $"<td>{H(string.Join(", ", o.Checks.Where(x => !x.Value).Select(x => x.Key)))}</td><td>{o.AgentMs}</td>" +
                $"<td>{H(o.Scores.Count == 0 ? "not_run" : string.Join("; ", o.Scores.Select(x => $"{x.Key}: {x.Value.Value}, valid={x.Value.Valid}")))}</td></tr>");
        html.Append("</table>");
        foreach (Outcome o in report.Outcomes)
            html.Append($"<h2>{H(o.CaseId)} / {o.Iteration}</h2><details><summary>Recorded response, tools and checks</summary>" +
                $"<pre>{H(JsonSerializer.Serialize(o, Cases.Json))}</pre></details>");
        html.Append("</html>");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, $"evidence-{report.RunId}.html");
        await File.WriteAllTextAsync(path, html.ToString());
        return path;
    }
}

public sealed record CasePair(string CaseId, int Iteration, bool BaselinePassed, bool CandidatePassed,
    string[] FailedChecks, long AgentMsDelta, Dictionary<string, double?> ScoreDeltas);
public sealed record Comparison(string BaselineRunId, string CandidateRunId, bool Accepted, CasePair[] Pairs);

public static class Comparisons
{
    public static Comparison Pair(RunReport baseline, RunReport candidate)
    {
        App.Require(baseline.ExitCode == 0 && baseline.Outcomes.All(x => x.Passed), "The accepted baseline must pass before comparison.");
        App.Require(baseline.DatasetHash == candidate.DatasetHash && baseline.DatasetVersion == candidate.DatasetVersion &&
            baseline.PromptHash == candidate.PromptHash && baseline.FixtureHash == candidate.FixtureHash &&
            baseline.JudgeDeployment == candidate.JudgeDeployment && baseline.JudgeModel == candidate.JudgeModel &&
            baseline.JudgeModelVersion == candidate.JudgeModelVersion && baseline.JudgeStatus == candidate.JudgeStatus &&
            baseline.EvaluationPackageVersion == candidate.EvaluationPackageVersion && baseline.MafPackageVersion == candidate.MafPackageVersion &&
            baseline.MinimumQualityScore == candidate.MinimumQualityScore && baseline.Repetitions == candidate.Repetitions &&
            (baseline.Mode == "azure") == (candidate.Mode == "azure"), "Comparison inputs changed beyond the assistant candidate.");
        var left = baseline.Outcomes.ToDictionary(x => (x.CaseId, x.Iteration));
        var right = candidate.Outcomes.ToDictionary(x => (x.CaseId, x.Iteration));
        App.Require(left.Count == right.Count && left.Keys.All(right.ContainsKey), "Case IDs and repetitions must match exactly.");
        var pairs = left.Select(x =>
        {
            Outcome b = x.Value, c = right[x.Key];
            return new CasePair(c.CaseId, c.Iteration, b.Passed, c.Passed,
                c.Checks.Where(kv => !kv.Value).Select(kv => kv.Key).ToArray(), c.AgentMs - b.AgentMs,
                Contract.RequiredMetrics.ToDictionary(k => k, k =>
                    b.Scores.TryGetValue(k, out Score? bs) && c.Scores.TryGetValue(k, out Score? cs) && bs.Valid && cs.Valid
                        ? cs.Value - bs.Value : null));
        }).ToArray();
        return new(baseline.RunId, candidate.RunId, candidate.ExitCode == 0 && pairs.All(x => x.CandidatePassed), pairs);
    }

    public static string[] DriftSignals(RunReport baseline, RunReport candidate)
    {
        var signals = new List<string>();
        if (baseline.DatasetHash != candidate.DatasetHash || baseline.DatasetVersion != candidate.DatasetVersion) signals.Add("dataset-changed");
        if (baseline.FixtureHash != candidate.FixtureHash) signals.Add("fixture-contract-changed");
        if (baseline.PromptHash != candidate.PromptHash) signals.Add("prompt-changed");
        if (baseline.JudgeDeployment != candidate.JudgeDeployment || baseline.JudgeModelVersion != candidate.JudgeModelVersion ||
            baseline.JudgeModel != candidate.JudgeModel || baseline.JudgeStatus != candidate.JudgeStatus) signals.Add("judge-configuration-changed");
        if (baseline.AgentDeployment != candidate.AgentDeployment || baseline.AgentModelVersion != candidate.AgentModelVersion ||
            baseline.AgentModel != candidate.AgentModel) signals.Add("assistant-configuration-changed");
        var old = baseline.Outcomes.ToDictionary(x => (x.CaseId, x.Iteration));
        foreach (Outcome c in candidate.Outcomes)
        {
            if (!old.TryGetValue((c.CaseId, c.Iteration), out Outcome? b)) { signals.Add($"{c.CaseId}:new-sample"); continue; }
            bool sameEvidence = JsonSerializer.Serialize(b.Calls, Cases.Json) == JsonSerializer.Serialize(c.Calls, Cases.Json);
            if (!sameEvidence) signals.Add($"{c.CaseId}:recorded-tool-evidence-changed");
            if (b.Passed && !c.Passed) signals.Add($"{c.CaseId}:retained-behavior-failed;" +
                (sameEvidence ? "evidence-unchanged;inspect-generation-or-judge" : "inspect-tool-contract-first"));
        }
        if (old.Keys.Except(candidate.Outcomes.Select(x => (x.CaseId, x.Iteration))).Any()) signals.Add("samples-missing");
        return signals.Distinct().ToArray();
    }
}
