using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Unit16.Common;

public sealed record CaseOutcome(string Id, string Service, bool Passed, Evidence? Baseline,
    Evidence? Candidate, string Mode, ToolCall[]? ToolTrace, string ToolEvidence,
    Dictionary<string, bool> Checks, long ElapsedMs, string? ErrorType);
public sealed record SuiteReport(string Suite, string Qualification, bool Passed, int Count,
    int Failures, DateTimeOffset CreatedAtUtc, CaseOutcome[] Cases);

public static class MigrationSuite
{
    public static async Task<SuiteReport> RunAsync(LabArguments args)
    {
        List<CaseOutcome> rows = [];
        using HttpEvidenceClient? http = args.Has("--http")
            ? new(args.Value("--base") ?? UnitSettings.Value("UNIT16_BASE", "http://127.0.0.1:8082")) : null;
        if (http is not null) await http.ReadinessAsync();
        foreach (MigrationCase sample in Contract.Cases)
        {
            LabConsole.Write("CASE", $"{sample.Id}: requested='{sample.Input}', oracle='{sample.ExpectedStatus}'");
            string service = Contract.Normalize(sample.Input);
            Stopwatch timer = Stopwatch.StartNew();
            Evidence? baseline = null, candidate = null;
            ToolCall[]? calls = null;
            string mode = "failed", toolEvidence = "missing";
            Dictionary<string, bool> checks = [];
            string? error = null;
            try
            {
                LabConsole.Write("LEGACY", "Adapt a synthetic recorded boundary; no legacy SDK is executing.");
                baseline = Contract.AdaptLegacy(Contract.LegacyRecording(service), service);
                CandidateResult result;
                if (http is not null) result = await http.ReadAsync(service);
                else
                {
                    using MafRuntime runtime = new(args.Has("--live"));
                    result = await runtime.ReadAsync(service);
                }
                candidate = result.Evidence; calls = result.Calls; mode = result.Mode;
                toolEvidence = calls is null ? "not_exposed_by_http_contract" : "recorded_by_application";
                if (args.Has("--inject-regression") && service == "vpn")
                {
                    LabConsole.Write("FAULT INJECTION", "Change the returned VPN status to healthy after the read.");
                    candidate = candidate with { Status = "healthy" };
                }
                checks["baselineMatchesOracle"] = baseline.Service == service && baseline.Status == sample.ExpectedStatus;
                checks["candidateMatchesOracle"] = candidate.Service == service && candidate.Status == sample.ExpectedStatus;
                checks["parity"] = baseline.Service == candidate.Service && baseline.Status == candidate.Status;
                bool known = Contract.Catalog.ContainsKey(service);
                if (args.Has("--require-azure") && known) checks["azureMode"] = mode == "azure";
                if (calls is not null)
                {
                    checks["readToolEvidence"] = known
                        ? calls.Length == 1 && calls[0].Name == nameof(StatusTools.GetServiceStatus) && calls[0].Argument == service
                        : calls.Length == 0;
                    checks["noWriteTools"] = calls.All(c => c.Name == nameof(StatusTools.GetServiceStatus));
                    checks["answerMatchesTool"] = !known || (calls.Length == 1 && candidate == calls[0].Result);
                }
                foreach ((string name, bool passed) in checks)
                    LabConsole.Write("CHECK", $"{(passed ? "PASS" : "FAIL")} | {sample.Id}/{name}");
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name;
                LabConsole.Write("CASE ERROR", $"{sample.Id}: {error}. The case cannot be accepted.");
            }
            timer.Stop();
            rows.Add(new(sample.Id, service, error is null && checks.Count > 0 && checks.Values.All(x => x),
                baseline, candidate, mode, calls, toolEvidence, checks, timer.ElapsedMilliseconds, error));
        }
        string qualification = args.Has("--live") || args.Has("--require-azure") ? "azure" :
            http is null ? "maf-runtime-with-fixture-model" : "http-contract";
        return new("unit16-contract-v2", qualification, rows.All(x => x.Passed), rows.Count,
            rows.Count(x => !x.Passed), DateTimeOffset.UtcNow, rows.ToArray());
    }
}

public sealed class HttpEvidenceClient : IDisposable
{
    private readonly HttpClient _client;
    public HttpEvidenceClient(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out Uri? uri) ||
            uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("Use an absolute HTTP(S) base URL without credentials or a query.");
        _client = new HttpClient { BaseAddress = uri,
            Timeout = TimeSpan.FromSeconds(int.Parse(UnitSettings.Value("UNIT16_HTTP_TIMEOUT_SECONDS", "25"))) };
    }
    public async Task ReadinessAsync()
    {
        LabConsole.Write("HTTP REQUEST", $"GET {new Uri(_client.BaseAddress!, "health/ready")}");
        using HttpResponseMessage response = await _client.GetAsync("health/ready");
        LabConsole.Write("HTTP RESPONSE", $"{(int)response.StatusCode} {response.StatusCode}");
        response.EnsureSuccessStatusCode();
        LabConsole.Write("MILESTONE", "Host is ready. Readiness alone does not qualify Azure inference.");
    }
    public async Task<CandidateResult> ReadAsync(string service)
    {
        LabConsole.Write("HTTP REQUEST", $"POST {new Uri(_client.BaseAddress!, "api/status")}");
        LabConsole.Data("HTTP JSON BODY", new { service });
        using HttpResponseMessage response = await _client.PostAsJsonAsync("api/status", new { service });
        LabConsole.Write("HTTP RESPONSE", $"{(int)response.StatusCode} {response.StatusCode}");
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        if (response.StatusCode == HttpStatusCode.BadRequest &&
            root.TryGetProperty("error", out JsonElement error) && error.GetString() == "unknown_service" &&
            !Contract.Catalog.ContainsKey(service))
        {
            LabConsole.Write("BOUNDARY", "HTTP 400 unknown_service is the expected explicit rejection.");
            return new(Contract.Unknown(service), "rejected_before_agent", null);
        }
        response.EnsureSuccessStatusCode();
        Evidence evidence = JsonSerializer.Deserialize<Evidence>(root, JsonOutput.Options)
            ?? throw new InvalidDataException("Empty HTTP evidence.");
        Contract.Validate(evidence, service);
        string mode = root.GetProperty("mode").GetString() ?? "missing";
        if (mode is not ("fixture" or "fixture-model" or "azure")) throw new InvalidDataException("Missing execution mode.");
        ToolCall[]? calls = root.TryGetProperty("toolTrace", out JsonElement trace) && trace.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<ToolCall[]>(trace, JsonOutput.Options) : null;
        string? release = root.TryGetProperty("release", out JsonElement r) ? r.GetString() : null;
        string? prompt = root.TryGetProperty("promptVersion", out JsonElement p) ? p.GetString() : null;
        LabConsole.Data("HTTP EVIDENCE", new { evidence.Service, evidence.Status, mode, release, promptVersion = prompt });
        if (calls is null) LabConsole.Write("EVIDENCE GAP", "Host does not expose toolTrace; no tool-execution claim is inferred.");
        return new(evidence, mode, calls, release, prompt);
    }
    public void Dispose() => _client.Dispose();
}
