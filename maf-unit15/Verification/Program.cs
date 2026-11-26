using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using Maf.Unit15.Application;
using Maf.Unit15.Examples;
using Maf.Unit15.Infrastructure;

string root = EnvironmentFile.Load();
string temporary = Path.Combine(Path.GetTempPath(), "maf-unit15-verify-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);
var checks = new List<string>();
string configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent!.Name;
string dotnet = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "../../../", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
if (!File.Exists(dotnet)) dotnet = "dotnet";
try
{
    using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "examples.json")));
    var projects = manifest.RootElement.GetProperty("examples").EnumerateArray().ToDictionary(
        x => x.GetProperty("id").GetString()!, x => x.GetProperty("project").GetString()!);
    var results = new Dictionary<string, JsonElement>();
    foreach (var (id, project) in projects)
    {
        results[id] = await ExecuteAsync(project, id, "fixture");
        checks.Add(id + ": fixture project completed");
    }
    Check(results["v04"].GetProperty("recovery")[0].GetProperty("outcome").GetString() == "duplicate", "Recovery did not deduplicate.");
    Check(results["v04"].GetProperty("final").GetProperty("reviewItems").GetArrayLength() == 1, "Review effect repeated.");
    Check(results["v05"].GetProperty("invalidCaptureDetected").GetBoolean(), "Scope validation was not demonstrated.");
    Check(results["v06"].GetProperty("checks").GetArrayLength() == 4, "Contract cases are missing.");
    Check(!results["v08"].GetProperty("modelCalled").GetBoolean(), "Dependency check called a model.");
    Check(results["v09"].GetProperty("successful").GetProperty("process").GetProperty("state").GetString() == "Verified", "Repair state machine failed.");
    Check(results["v10"].GetProperty("auditState").GetProperty("auditRecords").GetInt32() == 1, "Audit repeated its effect.");
    bool stringSchemaRejected = false;
    var stringSchema = new RecordedEvent(Guid.NewGuid().ToString(), "AssessmentRecorded", 1, "strict-schema", new("strict-schema", "vpn", "review_required", "fixture"));
    try { EventContract.Parse(EventContract.Serialize(stringSchema).Replace("\"schemaVersion\":1", "\"schemaVersion\":\"1\"")); }
    catch (InvalidDataException) { stringSchemaRejected = true; }
    Check(stringSchemaRejected, "Schema version must be a JSON integer.");
    await ExecuteAsync("Exercises/Ex01.TransactionBoundaries/Ex01.TransactionBoundaries.csproj", "exercise01", "fixture");
    var reporting = await ExecuteAsync("Exercises/Ex02.ReportingConsumer/Ex02.ReportingConsumer.csproj", "exercise02", "fixture");
    Check(reporting.GetProperty("reporting").GetProperty("counts").GetProperty("degraded").GetInt64() == 1, "Reporting replay inflated counters.");
    checks.Add("Two instructor solutions passed");
    checks.Add("Schema version JSON type enforced");

    // Verify that consumer callbacks roll back with the inbox insert.
    var rollback = new Inbox(Path.Combine(temporary, "rollback.db"), ReviewConsumer.Schema);
    var e = new RecordedEvent(Guid.NewGuid().ToString(), "AssessmentRecorded", 1, "rollback", new("rollback", "vpn", "review_required", "fixture"));
    try { await rollback.ConsumeAsync(EventContract.Serialize(e), (_, _, _) => throw new IOException("Injected callback failure"), default); }
    catch (IOException) { }
    Check(await rollback.CountAsync("inbox", default) == 0, "A failed consumer retained its inbox marker.");
    checks.Add("Atomic consumer rollback passed");

    // Original event bytes, including whitespace and snake_case data, remain replayable.
    string legacyPayload = """
        {"correlationId": "legacy-001", "data": {"decision": "review_required", "evidence_mode": "fixture", "operation_id": "legacy-001", "service": "vpn"}, "eventId": "110bded1-91d0-44e6-b739-a1bd67ecf190", "eventType": "AssessmentRecorded", "schemaVersion": 1}
        """;
    var legacy = new LabStores(Path.Combine(temporary, "legacy"));
    using (var db = Sql.Open(legacy.Producer.DatabasePath))
    {
        using var a = Sql.Command(db, null, "INSERT INTO assessments VALUES ('legacy-001','vpn','review_required','fixture')"); a.ExecuteNonQuery();
        using var b = Sql.Command(db, null, "INSERT INTO outbox(event_id,payload) VALUES ($id,$payload)", ("$id", EventContract.Parse(legacyPayload).EventId), ("$payload", legacyPayload)); b.ExecuteNonQuery();
    }
    await legacy.Review.ConsumeAsync(legacyPayload, default);
    var legacyRecovery = await legacy.Dispatcher.DispatchAsync(false, default);
    Check(legacyRecovery[0].Outcome == "duplicate" && (await legacy.InspectAsync(default)).ReviewItems.Count == 1, "Legacy event recovery failed.");
    checks.Add("Existing SQLite schema and original event bytes preserved");

    string address = AvailableAddress();
    var host = Start("EvidenceHost/Maf.Unit15.EvidenceHost.csproj", new[] { "--mode", "fixture" }, address);
    Task<string> hostOutput = host.StandardOutput.ReadToEndAsync(), hostErrors = host.StandardError.ReadToEndAsync();
    try
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        bool ready = false;
        for (int i = 0; i < 80; i++)
        {
            if (host.HasExited) throw new InvalidOperationException("Evidence host exited: " + await hostErrors);
            try { using var response = await http.GetAsync(address + "/health/ready"); ready = response.IsSuccessStatusCode; }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            if (ready) break;
            await Task.Delay(100);
        }
        Check(ready, "Evidence host did not become ready.");
        foreach (string id in new[] { "v02", "v04", "v07" })
        {
            var result = await ExecuteAsync(projects[id], id + "-http", "http", address);
            if (id == "v07")
                Check(result.GetProperty("evidenceResponse").GetProperty("traceId").GetString() == result.GetProperty("traceId").GetString(), "HTTP trace context did not propagate.");
            checks.Add(id + ": real .NET HTTP fixture passed");
        }
    }
    finally
    {
        if (!host.HasExited) host.Kill(entireProcessTree: true);
        await host.WaitForExitAsync();
        await hostOutput; await hostErrors; host.Dispose();
    }
    var report = new { status = "passed", checks, azureInferenceTested = false, toolchain = ".NET 10 and C#" };
    string output = Path.Combine(root, "artifacts", "verification-latest.json");
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions(EventContract.Json) { WriteIndented = true }));
    Console.WriteLine("RESULT " + EventContract.Serialize(report));
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }
finally { Directory.Delete(temporary, recursive: true); }

void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
Process Start(string project, IEnumerable<string> arguments, string? hostUrl = null)
{
    string directory = Path.GetDirectoryName(project)!;
    string dll = Path.Combine(root, directory, "bin", configuration, "net10.0", Path.GetFileNameWithoutExtension(project) + ".dll");
    if (!File.Exists(dll)) throw new FileNotFoundException("Build Maf.Unit15.slnx before verification.", dll);
    var start = new ProcessStartInfo(dotnet) { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    start.ArgumentList.Add(dll);
    foreach (string argument in arguments) start.ArgumentList.Add(argument);
    if (hostUrl is not null) start.Environment["ASPNETCORE_URLS"] = hostUrl;
    return Process.Start(start) ?? throw new InvalidOperationException("Could not start project.");
}
async Task<JsonElement> ExecuteAsync(string project, string id, string mode, string? address = null)
{
    var arguments = new List<string> { "--mode", mode, "--service", "vpn", "--state", Path.Combine(temporary, id) };
    if (address is not null) arguments.AddRange(new[] { "--base", address });
    using var process = Start(project, arguments);
    Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
    using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(45));
    try { await process.WaitForExitAsync(limit.Token); }
    catch { if (!process.HasExited) process.Kill(true); throw; }
    string text = await output, errors = await error;
    if (process.ExitCode != 0) throw new InvalidOperationException(id + " failed: " + errors);
    string line = text.Split('\n').Last(x => x.StartsWith("RESULT ", StringComparison.Ordinal));
    using var report = JsonDocument.Parse(line[7..]);
    Check(report.RootElement.GetProperty("status").GetString() == "ok", id + " reported failure.");
    Console.WriteLine("PASS " + id);
    return report.RootElement.GetProperty("result").Clone();
}
string AvailableAddress()
{
    foreach (int port in new[] { 8082, 8080 })
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        try { listener.Start(); listener.Stop(); return "http://127.0.0.1:" + port; }
        catch (SocketException) { listener.Stop(); }
    }
    throw new InvalidOperationException("Ports 8080 and 8082 are busy; stop the prior fixture host before verification.");
}
