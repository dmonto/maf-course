using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Unit14;

public sealed record ProbeOptions(string BaseUri, string Release, string Mode = "fixture",
    int Requests = 4, int Parallel = 1, int TimeoutSeconds = 120, string Output = "smoke-report.json")
{
    public void Validate()
    {
        if (!Uri.TryCreate(BaseUri, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host) ||
            uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("Use an HTTP(S) origin URL without credentials, path, query or fragment.");
        if (uri.Scheme == "http" && uri.Host is not ("127.0.0.1" or "localhost" or "[::1]" or "::1"))
            throw new ArgumentException("Remote targets require HTTPS.");
        if (string.IsNullOrWhiteSpace(Release)) throw new ArgumentException("Supply the expected --release.");
        if (Mode is not ("fixture" or "azure")) throw new ArgumentException("Use fixture or azure mode.");
        if (Requests is < 1 or > 20 || Parallel is < 1 or > 4)
            throw new ArgumentException("Use 1..20 requests and 1..4 parallel workers.");
        if (TimeoutSeconds is < 1 or > 300) throw new ArgumentException("Use a request timeout of 1..300 seconds.");
        if (string.IsNullOrWhiteSpace(Output)) throw new ArgumentException("Supply an output path.");
    }

    public static ProbeOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string[] names = ["--base", "--release", "--mode", "--requests", "--parallel", "--timeout", "--output"];
        for (int i = 0; i < args.Length; i += 2)
        {
            if (!names.Contains(args[i]) || i + 1 >= args.Length || !values.TryAdd(args[i], args[i + 1]))
                throw new ArgumentException("Use each supported probe option once, followed by its value.");
        }
        int Number(string key, int fallback) => !values.TryGetValue(key, out string? value) ? fallback :
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number :
            throw new ArgumentException("Probe request, parallel and timeout options require integers.");
        var options = new ProbeOptions(values.GetValueOrDefault("--base", "http://127.0.0.1:8080"),
            values.GetValueOrDefault("--release", ""), values.GetValueOrDefault("--mode", "fixture"),
            Number("--requests", 4), Number("--parallel", 1), Number("--timeout", 120),
            values.GetValueOrDefault("--output", "smoke-report.json"));
        options.Validate();
        return options;
    }
}

public static class DeploymentProbe
{
    public static async Task<ProbeResult> RunAsync(ProbeOptions options, CancellationToken cancellationToken = default)
    {
        options.Validate();
        using var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false };
        // Reuse one connection pool for the entire probe, including concurrent requests.
        using var client = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUri), Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (Environment.GetEnvironmentVariable("SMOKE_BEARER_TOKEN") is { Length: > 0 } token)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        LabConsole.Write("PROBE", $"Target {options.BaseUri} | expected release={options.Release} mode={options.Mode} | requests={options.Requests} parallel={options.Parallel}");
        var checks = new Dictionary<string, object?>();
        var rows = new List<Dictionary<string, object?>>();
        var report = new Dictionary<string, object?>
        {
            ["release"] = options.Release, ["mode"] = options.Mode, ["passed"] = false,
            ["requestTimeoutSeconds"] = options.TimeoutSeconds, ["checks"] = checks, ["results"] = rows
        };

        async Task<(int Code, JsonElement Body, double ElapsedMs)> RequestAsync(string path,
            object? payload = null, double? timeoutSeconds = null)
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds ?? options.TimeoutSeconds));
            using var request = new HttpRequestMessage(payload is null ? HttpMethod.Get : HttpMethod.Post, path);
            LabConsole.Write("HTTP REQUEST", $"{request.Method} {options.BaseUri.TrimEnd('/')}{path}");
            if (payload is not null) LabConsole.Write("HTTP JSON BODY", JsonSerializer.Serialize(payload));
            if (payload is not null) request.Content = JsonContent.Create(payload);
            var watch = Stopwatch.StartNew();
            try
            {
                using var response = await client.SendAsync(request, budget.Token);
                JsonElement body;
                try
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(budget.Token);
                    using var document = await JsonDocument.ParseAsync(stream, cancellationToken: budget.Token);
                    body = document.RootElement.Clone();
                }
                catch (JsonException) when (!response.IsSuccessStatusCode) { body = JsonSerializer.SerializeToElement(new { }); }
                LabConsole.Write("HTTP RESPONSE", $"{(int)response.StatusCode} in {watch.Elapsed.TotalMilliseconds:F0} ms");
                return ((int)response.StatusCode, body, watch.Elapsed.TotalMilliseconds);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Probe request deadline exceeded.");
            }
        }

        void ValidateMetadata(JsonElement body)
        {
            LabConsole.Write("CHECK", $"Metadata expected release={options.Release} mode={options.Mode} promptVersion={Lab.PromptVersion}; observed release={Text(body, "release")} mode={Text(body, "mode")} promptVersion={Text(body, "promptVersion")}");
            if (body.ValueKind != JsonValueKind.Object || Text(body, "release") != options.Release ||
                Text(body, "mode") != options.Mode || Text(body, "promptVersion") != Lab.PromptVersion ||
                string.IsNullOrEmpty(Text(body, "instance")))
                throw new InvalidOperationException("Unexpected deployment metadata.");
        }

        async Task<Dictionary<string, object?>> CheckAsync(int index)
        {
            string service = index % 2 == 0 ? "vpn" : "email";
            var watch = Stopwatch.StartNew();
            var row = new Dictionary<string, object?> { ["service"] = service, ["httpStatus"] = null, ["passed"] = false };
            try
            {
                var (code, body, elapsed) = await RequestAsync("/api/status", new { service });
                row["httpStatus"] = code;
                row["elapsedMs"] = Math.Round(elapsed, 2);
                if (code == 200)
                {
                    ValidateMetadata(body);
                    row["instance"] = Text(body, "instance");
                    string status = service == "vpn" ? "degraded" : "healthy";
                    string summary = service == "vpn" ? "Synthetic evidence: VPN connectivity is degraded." : "Synthetic evidence: email is healthy.";
                    row["passed"] = Text(body, "service") == service && Text(body, "status") == status &&
                        Text(body, "summary") == summary && !string.IsNullOrEmpty(Text(body, "requestId"));
                }
                else CopyDiagnostics(body, row);
            }
            catch (Exception error)
            {
                row["failureType"] = error.GetType().Name;
                row["elapsedMs"] = Math.Round(watch.Elapsed.TotalMilliseconds, 2);
            }
            LabConsole.Write("CHECK", $"{(Equals(row["passed"], true) ? "PASS" : "FAIL")} | {service} HTTP={row["httpStatus"]}");
            return row;
        }

        string stage = "readiness", path = "/health/ready";
        LabConsole.Write("MILESTONE", "Stage 1/3 readiness: wait (max 45 s) for the expected release, mode and prompt version.");
        try
        {
            var startup = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    var (code, body, _) = await RequestAsync(path, timeoutSeconds:
                        Math.Max(0.01, Math.Min(Math.Min(options.TimeoutSeconds, 10), 45 - startup.Elapsed.TotalSeconds)));
                    checks[stage] = new { httpStatus = code, passed = false };
                    if (code == 200)
                    {
                        ValidateMetadata(body);
                        if (Text(body, "status") != "ok") throw new InvalidOperationException("Unexpected readiness status.");
                        checks[stage] = new { httpStatus = code, passed = true };
                        break;
                    }
                    if (code is >= 300 and < 400 or 401 or 403)
                        throw new InvalidOperationException("Probe cannot access the expected health response.");
                }
                catch (Exception error) when (error is HttpRequestException or TimeoutException) { }
                if (startup.Elapsed.TotalSeconds >= 45) throw new TimeoutException("Readiness deadline exceeded.");
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
            LabConsole.Write("CHECK", "PASS | readiness");
            LabConsole.Write("MILESTONE", "Stage 2/3 liveness.");
            stage = "liveness"; path = "/health/live";
            var (liveCode, liveBody, _) = await RequestAsync(path, timeoutSeconds: Math.Min(options.TimeoutSeconds, 10));
            checks[stage] = new { httpStatus = liveCode, passed = false };
            if (liveCode != 200 || Text(liveBody, "status") != "ok") throw new InvalidOperationException("Liveness check failed.");
            ValidateMetadata(liveBody);
            checks[stage] = new { httpStatus = liveCode, passed = true };
            LabConsole.Write("CHECK", "PASS | liveness");
            LabConsole.Write("MILESTONE", "Stage 3/3 functional: POST /api/status and compare the evidence contract.");
            stage = "functional"; path = "/api/status";
            for (int start = 0; start < options.Requests; start += options.Parallel)
            {
                var batch = await Task.WhenAll(Enumerable.Range(start, Math.Min(options.Parallel, options.Requests - start)).Select(CheckAsync));
                rows.AddRange(batch);
                // Retain failed results and stop before issuing another expensive batch.
                if (batch.Any(row => !Equals(row["passed"], true))) break;
            }
            if (rows.Any(row => !Equals(row["passed"], true))) throw new InvalidOperationException("Functional request failed.");
            double[] samples = rows.Select(row => (double)row["elapsedMs"]!).Order().ToArray();
            report["p95Ms"] = samples[(int)Math.Ceiling(0.95 * samples.Length) - 1];
            report["instancesObserved"] = rows.Select(row => (string)row["instance"]!).Distinct().Order().ToArray();
            report["passed"] = true;
        }
        catch (Exception error)
        {
            // Store only failure classes and allowlisted metadata, never messages or model bodies.
            report["failureType"] = error.GetType().Name;
            report["failureStage"] = stage;
            report["failurePath"] = path;
            LabConsole.Write("FAILURE", $"{error.GetType().Name} at stage {stage} ({path})");
        }
        Lab.Save(options.Output, report);
        bool passed = Equals(report["passed"], true);
        Console.WriteLine($"{(passed ? "PASS" : $"FAIL [{stage}]")}: {options.Output}");
        LabConsole.Write("CONCLUSION", passed ? "Readiness, liveness and every functional check passed." : $"The probe failed at {stage}; this deployment is not qualified.");
        return new(passed ? 0 : 1, options.Output, JsonSerializer.SerializeToElement(report, Lab.Json));
    }

    private static string? Text(JsonElement body, string key) => body.ValueKind == JsonValueKind.Object &&
        body.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static void CopyDiagnostics(JsonElement body, Dictionary<string, object?> row)
    {
        if (body.ValueKind != JsonValueKind.Object) return;
        foreach (var (key, values) in new (string, string[])[]
        {
            ("error", ["request_cancelled", "agent_unavailable", "busy", "unknown_service"]),
            ("phase", ["identity", "model", "contract", "fixture"]),
            ("reason", ["client_disconnected", "application_stopping", "request_deadline"])
        })
            if (Text(body, key) is { } value && values.Contains(value)) row[key] = value;
        if (Text(body, "requestId") is { } requestId && Regex.IsMatch(requestId, "^[a-f0-9]{32}$")) row["requestId"] = requestId;
        if (Text(body, "failureType") is { } failureType && Regex.IsMatch(failureType, "^[A-Za-z0-9.]{1,128}$")) row["failureType"] = failureType;
        foreach (var (source, target, min, max) in new[]
        {
            ("dependencyStatus", "dependencyStatus", 100d, 599d),
            ("timeoutSeconds", "timeoutSeconds", 1d, 180d),
            ("elapsedMs", "serverElapsedMs", 0d, 1_000_000d)
        })
            if (body.TryGetProperty(source, out var value) && value.ValueKind == JsonValueKind.Number &&
                value.TryGetDouble(out double number) && number >= min && number <= max) row[target] = number;
    }
}
