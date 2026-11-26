using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unit14;

public static class ProbeChecks
{
    public static async Task RunAsync(Context context)
    {
        foreach (string scenario in new[] { "success", "server-deadline", "functional-timeout", "liveness-timeout",
            "body-timeout", "wrong-release", "wrong-summary", "redirect", "unauthorized", "invalid-json" })
        {
            int seen = 0;
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            await using var app = builder.Build();
            app.MapGet("/health/{kind}", async (HttpContext http, string kind) =>
            {
                if (scenario == "redirect") { http.Response.Redirect("/login"); return; }
                if (scenario == "unauthorized") { http.Response.StatusCode = 401; return; }
                if (scenario == "liveness-timeout" && kind == "live") await Task.Delay(1500, http.RequestAborted);
                await http.Response.WriteAsJsonAsync(new { status = "ok", release = "14.1.0", mode = "azure",
                    promptVersion = "status-v1", instance = "synthetic" }, http.RequestAborted);
            });
            app.MapPost("/api/status", async (HttpContext http) =>
            {
                Interlocked.Increment(ref seen);
                var request = await http.Request.ReadFromJsonAsync<StatusRequest>(http.RequestAborted);
                if (scenario == "functional-timeout") await Task.Delay(1500, http.RequestAborted);
                if (scenario == "body-timeout")
                {
                    http.Response.ContentType = "application/json";
                    await http.Response.WriteAsync("{", http.RequestAborted);
                    await http.Response.Body.FlushAsync(http.RequestAborted);
                    await Task.Delay(1500, http.RequestAborted);
                    return;
                }
                if (scenario == "server-deadline")
                {
                    http.Response.StatusCode = 504;
                    await http.Response.WriteAsJsonAsync(new { requestId = new string('a', 32), error = "request_cancelled",
                        phase = "identity", reason = "request_deadline", timeoutSeconds = 90, elapsedMs = 90000,
                        message = "unit14-sensitive-marker" }, http.RequestAborted);
                    return;
                }
                if (scenario == "invalid-json")
                {
                    await http.Response.WriteAsync("unit14-sensitive-marker", http.RequestAborted);
                    return;
                }
                string service = request!.Service!;
                bool vpn = service == "vpn";
                await http.Response.WriteAsJsonAsync(new { requestId = new string('b', 32), service,
                    status = vpn ? "degraded" : "healthy",
                    summary = scenario == "wrong-summary" ? "wrong" : vpn ? "Synthetic evidence: VPN connectivity is degraded." : "Synthetic evidence: email is healthy.",
                    release = scenario == "wrong-release" ? "unexpected" : "14.1.0", mode = "azure",
                    promptVersion = "status-v1", instance = "synthetic" }, http.RequestAborted);
            });
            await app.StartAsync();
            try
            {
                string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
                string output = Path.Combine(context.Artifacts, "probe checks", scenario + ".json");
                var result = await DeploymentProbe.RunAsync(new ProbeOptions(address, "14.1.0", "azure",
                    Requests: scenario == "success" ? 5 : 4, Parallel: scenario == "success" ? 2 : 1,
                    TimeoutSeconds: 1, Output: output));
                string raw = await File.ReadAllTextAsync(output);
                Lab.Require(!raw.Contains("unit14-sensitive-marker"), "Probe leaked response content.");
                var report = result.Report;
                if (scenario == "success")
                {
                    Lab.Require(result.ExitCode == 0 && report.GetProperty("passed").GetBoolean() && seen == 5 &&
                        report.GetProperty("results").GetArrayLength() == 5 && report.GetProperty("p95Ms").GetDouble() >= 0,
                        "Probe rejected a valid concurrent response batch.");
                    continue;
                }
                Lab.Require(result.ExitCode == 1 && !report.GetProperty("passed").GetBoolean(), "Probe accepted a failing deployment.");
                if (scenario is "redirect" or "unauthorized")
                {
                    Lab.Require(report.GetProperty("failureStage").GetString() == "readiness" && seen == 0,
                        "Probe accepted an inaccessible health response.");
                }
                else if (scenario == "liveness-timeout")
                {
                    Lab.Require(report.GetProperty("failureStage").GetString() == "liveness" &&
                        report.GetProperty("failurePath").GetString() == "/health/live" && seen == 0,
                        "Probe lost the liveness timeout boundary.");
                }
                else
                {
                    Lab.Require(report.GetProperty("failureStage").GetString() == "functional" && seen == 1 &&
                        report.GetProperty("results").GetArrayLength() == 1, "Probe continued after a failed functional batch.");
                    var row = report.GetProperty("results")[0];
                    if (scenario == "server-deadline") Lab.Require(row.GetProperty("httpStatus").GetInt32() == 504 &&
                        row.GetProperty("phase").GetString() == "identity", "Probe lost safe server diagnostics.");
                    if (scenario is "functional-timeout" or "body-timeout") Lab.Require(
                        row.GetProperty("failureType").GetString() == "TimeoutException", "Probe did not bound the complete HTTP request.");
                }
            }
            finally { await app.StopAsync(); }
        }

        foreach (string[] args in new[]
        {
            new[] { "--release", "14.1.0", "--base", "http://example.invalid" },
            new[] { "--release", "14.1.0", "--base", "https://user:secret@example.invalid" },
            new[] { "--release", "14.1.0", "--base", "https://example.invalid/path" },
            new[] { "--release", "14.1.0", "--requests", "0" },
            new[] { "--release", "14.1.0", "--parallel", "5" },
            new[] { "--release", "14.1.0", "--timeout", "0" },
            new[] { "--release", "14.1.0", "--timeout", "bad" },
            new[] { "--release", "14.1.0", "--unknown", "value" },
            new[] { "--release", "14.1.0", "--release", "other" },
            new[] { "--base" }, Array.Empty<string>()
        })
        {
            bool rejected = false;
            try { ProbeOptions.Parse(args); } catch (ArgumentException) { rejected = true; }
            Lab.Require(rejected, "Invalid probe arguments were accepted.");
        }
        context.Save("probe-checks.json", new { passed = true, scenarios = 10, invalidArguments = 11 });
    }
}
