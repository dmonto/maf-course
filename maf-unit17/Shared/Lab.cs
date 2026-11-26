using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Unit17;

public static class Lab
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static void Log(string kind, string text) => Console.WriteLine($"[{kind}] {text}");
    public static void Emit(object value) => Console.WriteLine("RESULT " + JsonSerializer.Serialize(value, Json));
    public static void Check(string label, bool passed)
    {
        Log("CHECK", $"{(passed ? "PASS" : "FAIL")} {label}");
        if (!passed) throw new InvalidOperationException(label);
    }
    public static void Block(string label, Action action)
    {
        try { action(); } catch (PolicyException e) { Log("BLOCKED", e.Message); Log("CHECK", $"PASS {label}: {e.Message}"); return; }
        throw new InvalidOperationException("Expected rejection: " + label);
    }
    public static string Env(string key, string fallback) => Environment.GetEnvironmentVariable(key) ?? fallback;
    public static void LoadEnv()
    {
        var file = Path.Combine(Environment.CurrentDirectory, ".env");
        if (!File.Exists(file)) return;
        foreach (var line in File.ReadLines(file))
        {
            var text = line.Trim(); int i = text.IndexOf('=');
            if (text.StartsWith('#') || i <= 0) continue;
            var key = text[..i].Trim(); var value = text[(i + 1)..].Trim().Trim('"', '\'');
            if (Environment.GetEnvironmentVariable(key) is null) Environment.SetEnvironmentVariable(key, value);
        }
    }
}
public sealed class PolicyException(string message) : Exception(message) { }
public sealed record Evidence(string Service, string Status, string Summary, string Mode, string? RequestId, string? Release);
public sealed record CaseRecord(string CaseId, string Service, Evidence Evidence, string State, long Expires, string? Reviewer, string? TicketId);
public static class EvidenceSource
{
    public static async Task<Evidence> ReadAsync(string service, string? baseUrl, bool requireAzure)
    {
        if (service is not ("vpn" or "email")) throw new PolicyException("unsupported_service");
        string status = service == "vpn" ? "degraded" : "healthy";
        string summary = service == "vpn" ? "Synthetic evidence: VPN connectivity is degraded." : "Synthetic evidence: email is healthy.";
        if (baseUrl is null)
        {
            if (requireAzure) throw new PolicyException("azure_evidence_required");
            Lab.Log("FIXTURE", $"Read synthetic catalog: {service}; no HTTP or MAF call");
            return new(service, status, summary, "fixture", null, null);
        }
        if (baseUrl.TrimEnd('/') is not ("http://127.0.0.1:8080" or "http://127.0.0.1:8082"))
            throw new PolicyException("unsupported_host");
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(195) };
        Lab.Log("HTTP REQUEST", $"GET {client.BaseAddress}health/ready");
        using var health = await client.GetAsync("/health/ready");
        Lab.Log("HTTP RESPONSE", $"{(int)health.StatusCode} {health.StatusCode}");
        health.EnsureSuccessStatusCode();
        using var healthJson = JsonDocument.Parse(await health.Content.ReadAsStringAsync());
        var root = healthJson.RootElement;
        string? mode = root.GetProperty("mode").GetString();
        if (root.GetProperty("status").GetString() != "ok" || mode is not ("fixture" or "azure") || (requireAzure && mode != "azure"))
            throw new PolicyException("host_mode_or_health_invalid");
        string? release = root.GetProperty("release").GetString();
        Lab.Log("MILESTONE", $"Host ready; mode={mode}; release={release}");
        Lab.Log("HTTP REQUEST", $"POST {client.BaseAddress}api/status {JsonSerializer.Serialize(new { service }, Lab.Json)}");
        if (mode == "azure") Lab.Log("REMOTE MAF", "Unit 14 host Azure branch: AsAIAgent / AIFunctionFactory.Create / RunAsync (server-side; not local calls)");
        else Lab.Log("FIXTURE", "Unit 14 host fixture branch; no model inference");
        using var response = await client.PostAsJsonAsync("/api/status", new { service });
        Lab.Log("HTTP RESPONSE", $"{(int)response.StatusCode} {response.StatusCode}");
        response.EnsureSuccessStatusCode();
        var evidence = await response.Content.ReadFromJsonAsync<Evidence>(Lab.Json) ?? throw new PolicyException("empty_evidence");
        if (evidence.Service != service || evidence.Status != status || evidence.Summary != summary || evidence.Mode != mode || evidence.Release != release || string.IsNullOrWhiteSpace(evidence.RequestId))
            throw new PolicyException("evidence_contract_failed");
        Lab.Log("CHECK", $"PASS host evidence contract; requestId={evidence.RequestId}");
        return evidence;
    }
}
public sealed class Store : IDisposable
{
    private readonly SqliteConnection db;
    public Store(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        db = new(new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true, DefaultTimeout = 10, Pooling = false }.ToString()); db.Open();
        using (var guard = db.CreateCommand())
        {
            guard.CommandText = "PRAGMA user_version";
            long version = (long)guard.ExecuteScalar()!;
            guard.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
            long tables = (long)guard.ExecuteScalar()!;
            if (version != 1701 && (version != 0 || tables != 0))
            { db.Dispose(); throw new PolicyException("incompatible_database_use_a_new_file"); }
        }
        using var init = db.BeginTransaction(deferred:false);
        using var cmd = db.CreateCommand(); cmd.Transaction = init; cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS cases(id TEXT PRIMARY KEY, service TEXT NOT NULL, evidence TEXT NOT NULL,
                state TEXT NOT NULL, expires INTEGER NOT NULL, reviewer TEXT, ticket_id TEXT);
            CREATE TABLE IF NOT EXISTS tickets(case_id TEXT PRIMARY KEY REFERENCES cases(id), ticket_id TEXT UNIQUE NOT NULL);
            PRAGMA user_version=1701;
            """; cmd.ExecuteNonQuery(); init.Commit();
        Lab.Log("STORE", $"SQLite database: {path}");
    }
    private SqliteCommand Command(SqliteTransaction? tx, string sql, params (string Key, object? Value)[] args)
    {
        var command = db.CreateCommand(); command.Transaction = tx; command.CommandText = sql;
        foreach (var (key, value) in args) command.Parameters.AddWithValue(key, value ?? DBNull.Value);
        return command;
    }
    private CaseRecord? Get(string id, SqliteTransaction? tx = null)
    {
        using var cmd = Command(tx, "SELECT id,service,evidence,state,expires,reviewer,ticket_id FROM cases WHERE id=$id", ("$id",id));
        using var r = cmd.ExecuteReader(); if (!r.Read()) return null;
        return new(r.GetString(0),r.GetString(1),JsonSerializer.Deserialize<Evidence>(r.GetString(2),Lab.Json)!,r.GetString(3),r.GetInt64(4),r.IsDBNull(5)?null:r.GetString(5),r.IsDBNull(6)?null:r.GetString(6));
    }
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private static void ValidateId(string id)
    { if (id.Length is < 1 or > 64 || id.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) throw new PolicyException("invalid_case_id"); }
    public CaseRecord Inspect(string id)
    {
        ValidateId(id); using var tx = db.BeginTransaction(deferred:false);
        var item = Current(id,tx); tx.Commit(); Lab.Log("MILESTONE", $"{id}: {item.State}; ticket={item.TicketId ?? "none"}"); return item;
    }
    private CaseRecord Current(string id, SqliteTransaction tx)
    {
        var item = Get(id,tx) ?? throw new PolicyException("case_not_found");
        if ((item.State is "awaiting_review" or "approved") && Now >= item.Expires)
        {
            using var cmd = Command(tx,"UPDATE cases SET state='expired' WHERE id=$id",("$id",id));cmd.ExecuteNonQuery();
            item = item with { State="expired" }; Lab.Log("MILESTONE", $"{id}: expired");
        }
        return item;
    }
    public async Task<CaseRecord> StartAsync(string id, string service, int ttl, string? baseUrl=null, bool requireAzure=false)
    {
        ValidateId(id); if (ttl is < 0 or > 86400) throw new PolicyException("invalid_ttl");
        if (Get(id) is { } existing)
        {
            if (existing.Service != service) throw new PolicyException("case_id_conflict");
            if (requireAzure && existing.Evidence.Mode != "azure") throw new PolicyException("azure_evidence_required");
            return Inspect(id);
        }
        var evidence = await EvidenceSource.ReadAsync(service,baseUrl,requireAzure);
        Lab.Log("MILESTONE", $"Evidence validated: {service}/{evidence.Status}; mode={evidence.Mode}");
        using var tx = db.BeginTransaction(deferred:false);
        if (Get(id,tx) is { } concurrent)
        {
            if (concurrent.Service != service) throw new PolicyException("case_id_conflict");
            if (requireAzure && concurrent.Evidence.Mode != "azure") throw new PolicyException("azure_evidence_required");
            tx.Commit(); return concurrent;
        }
        string state = evidence.Status == "degraded" ? "awaiting_review" : "no_action";
        long expiry = Now + ttl;
        using var cmd = Command(tx,"INSERT INTO cases(id,service,evidence,state,expires) VALUES($id,$service,$evidence,$state,$expires)",("$id",id),("$service",service),("$evidence",JsonSerializer.Serialize(evidence,Lab.Json)),("$state",state),("$expires",expiry));
        cmd.ExecuteNonQuery(); tx.Commit(); Lab.Log("MILESTONE", $"{id}: {state}; no ticket created");
        return new(id,service,evidence,state,expiry,null,null);
    }
    public CaseRecord Review(string id, string reviewer, string decision)
    {
        ValidateId(id); if (reviewer != "lab-reviewer" || decision is not ("approve" or "reject")) throw new PolicyException("invalid_review_fixture");
        using var tx = db.BeginTransaction(deferred:false); var item = Current(id,tx);
        if (item.State != "awaiting_review") { tx.Commit(); throw new PolicyException("review_blocked_"+item.State); }
        string state = decision == "approve" ? "approved" : "rejected";
        using var cmd = Command(tx,"UPDATE cases SET state=$state,reviewer=$reviewer WHERE id=$id",("$state",state),("$reviewer",reviewer),("$id",id));cmd.ExecuteNonQuery();tx.Commit();
        Lab.Log("MILESTONE", $"{id}: {state}; reviewer={reviewer} (simulated identity)"); return item with { State=state, Reviewer=reviewer };
    }
    public CaseRecord Execute(string id)
    {
        ValidateId(id);using var tx = db.BeginTransaction(deferred:false);var item=Current(id,tx);
        if (item.State == "completed") { tx.Commit();Lab.Log("MILESTONE", $"Idempotent replay: {item.TicketId}");return item; }
        if (item.State != "approved") { tx.Commit(); throw new PolicyException("execution_blocked_"+item.State); }
        string ticket="SIM-"+id;
        using(var insert=Command(tx,"INSERT INTO tickets(case_id,ticket_id) VALUES($id,$ticket)",("$id",id),("$ticket",ticket)))insert.ExecuteNonQuery();
        using(var update=Command(tx,"UPDATE cases SET state='completed',ticket_id=$ticket WHERE id=$id",("$id",id),("$ticket",ticket)))update.ExecuteNonQuery();
        tx.Commit();Lab.Log("MILESTONE", $"Committed simulated ticket {ticket} and completed state in one SQLite transaction");
        return item with { State="completed", TicketId=ticket };
    }
    public long CountTickets(){ using var cmd=Command(null,"SELECT COUNT(*) FROM tickets");return (long)cmd.ExecuteScalar()!; }
    public void Dispose()=>db.Dispose();
}
public sealed class Args(string[] args)
{
    public bool Has(string name)=>args.Contains(name,StringComparer.Ordinal);
    public string Value(string name,string fallback){int i=Array.IndexOf(args,name);return i<0?fallback:i+1<args.Length?args[i+1]:throw new ArgumentException("Missing value: "+name);}
    public string? Optional(string name)=>Has(name)?Value(name,""):null;
    public int Int(string name,int fallback)=>int.Parse(Value(name,fallback.ToString(CultureInfo.InvariantCulture)),CultureInfo.InvariantCulture);
    public double Number(string name,double fallback)=>double.Parse(Value(name,fallback.ToString(CultureInfo.InvariantCulture)),CultureInfo.InvariantCulture);
}
