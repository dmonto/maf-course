namespace Maf.Unit11;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

public interface IIntakePolicy
{
    void Validate(SupportEvent e);
}

public sealed class CapacityException() : Exception("The durable queue has reached its admission limit.");

public class EventStore(string filename, int leaseSeconds = 90, IIntakePolicy? policy = null)
{
    public string Filename => filename;
    protected SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = filename, DefaultTimeout = 5, Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }
    protected static SqliteCommand Command(SqliteConnection c, string sql,
        params (string Name, object? Value)[] values)
    {
        var command = c.CreateCommand(); command.CommandText = sql;
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }
    public void Initialize()
    {
        using var c = Open();
        using var command = Command(c, """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS jobs (
                id TEXT PRIMARY KEY, payload TEXT NOT NULL, payload_hash TEXT NOT NULL,
                state TEXT NOT NULL, attempts INTEGER NOT NULL DEFAULT 0,
                accepted_ms INTEGER NOT NULL, available_ms INTEGER NOT NULL,
                lease_until_ms INTEGER NOT NULL DEFAULT 0, owner TEXT,
                result TEXT, error_code TEXT, first_text_ms REAL, processing_ms REAL
            );
            CREATE INDEX IF NOT EXISTS ix_jobs_ready ON jobs(state, available_ms);
            """);
        command.ExecuteNonQuery();
    }
    protected (SupportEvent Event, string Payload, string Hash) Prepare(SupportEvent e)
    {
        e.Validate(); policy?.Validate(e);
        e = e with { OccurredAtUtc = e.OccurredAtUtc.ToUniversalTime() };
        string payload = JsonSerializer.Serialize(e, LabJson.Options);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        return (e, payload, hash);
    }
    public virtual bool Accept(SupportEvent e)
    {
        var prepared = Prepare(e);
        e = prepared.Event; string payload = prepared.Payload, hash = prepared.Hash;
        using var c = Open();
        // Immediate write ownership keeps identity checks and insertion atomic.
        using var transaction = c.BeginTransaction(deferred: false);
        using var check = Command(c, "SELECT payload_hash FROM jobs WHERE id=$id;", ("$id", e.Id));
        check.Transaction = transaction;
        object? previous = check.ExecuteScalar();
        if (previous is string previousHash)
        {
            if (previousHash != hash) throw new InvalidDataException("Event ID already has a different payload.");
            transaction.Commit(); return false;
        }
        using var insert = Command(c, """
            INSERT INTO jobs(id,payload,payload_hash,state,accepted_ms,available_ms)
            VALUES($id,$payload,$hash,'Pending',$now,$now);
            """, ("$id", e.Id), ("$payload", payload), ("$hash", hash), ("$now", LabConfig.NowMs));
        insert.Transaction = transaction;
        insert.ExecuteNonQuery(); transaction.Commit(); return true;
    }
    public Claim? TryClaim()
    {
        long now = LabConfig.NowMs; string owner = Guid.NewGuid().ToString("N");
        using var c = Open();
        using var expire = Command(c, """
            UPDATE jobs SET state='Dead',error_code='AttemptLimitAfterCrash',owner=NULL
            WHERE state='Processing' AND lease_until_ms<=$now AND attempts>=3;
            """, ("$now", now));
        expire.ExecuteNonQuery();
        using var command = Command(c, """
            UPDATE jobs SET state='Processing',attempts=attempts+1,owner=$owner,lease_until_ms=$lease
            WHERE id=(SELECT id FROM jobs WHERE attempts<3 AND
                ((state='Pending' AND available_ms<=$now) OR
                 (state='Processing' AND lease_until_ms<=$now))
                ORDER BY accepted_ms,id LIMIT 1)
            RETURNING payload,attempts,accepted_ms;
            """, ("$owner", owner), ("$lease", now + leaseSeconds * 1000L), ("$now", now));
        using var reader = command.ExecuteReader();
        return reader.Read() ? new Claim(
            JsonSerializer.Deserialize<SupportEvent>(reader.GetString(0), LabJson.Options)!,
            reader.GetInt32(1), owner, reader.GetInt64(2)) : null;
    }
    public bool Complete(Claim claim, string result, double? firstTextMs, double processingMs)
    {
        using var c = Open();
        using var command = Command(c, """
            UPDATE jobs SET state='Completed',result=$result,error_code=NULL,
                first_text_ms=$first,processing_ms=$processing,owner=NULL
            WHERE id=$id AND state='Processing' AND owner=$owner AND lease_until_ms>$now;
            """, ("$id", claim.Event.Id), ("$owner", claim.Owner), ("$now", LabConfig.NowMs),
            ("$result", result), ("$first", firstTextMs), ("$processing", processingMs));
        return command.ExecuteNonQuery() == 1;
    }
    public string Fail(Claim claim, bool retryable, string errorCode)
    {
        string state = retryable && claim.Attempt < 3 ? "Pending" : "Dead";
        long now = LabConfig.NowMs;
        long delay = (long)Math.Pow(2, claim.Attempt) * 1000 + Random.Shared.Next(0, 500);
        using var c = Open();
        using var command = Command(c, """
            UPDATE jobs SET state=$state,available_ms=$available,error_code=$error,owner=NULL
            WHERE id=$id AND state='Processing' AND owner=$owner AND lease_until_ms>$now;
            """, ("$state", state), ("$available", now + delay), ("$error", errorCode),
            ("$id", claim.Event.Id), ("$owner", claim.Owner), ("$now", now));
        return command.ExecuteNonQuery() == 1 ? state : "LeaseLost";
    }
    public IReadOnlyList<JobView> Read(string? id = null)
    {
        using var c = Open();
        using var command = Command(c, """
            SELECT id,state,attempts,accepted_ms,result,error_code,first_text_ms,processing_ms
            FROM jobs WHERE ($id IS NULL OR id=$id) ORDER BY accepted_ms,id LIMIT 100;
            """, ("$id", id));
        using var reader = command.ExecuteReader(); var rows = new List<JobView>();
        while (reader.Read()) rows.Add(new(reader.GetString(0), reader.GetString(1),
            reader.GetInt32(2), reader.GetInt64(3), reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetDouble(6),
            reader.IsDBNull(7) ? null : reader.GetDouble(7)));
        return rows;
    }
}
