using Maf.Unit15.Application;
using Microsoft.Data.Sqlite;

namespace Maf.Unit15.Infrastructure;

public sealed class AssessmentStore : IAssessmentStore, IOutboxStore
{
    public const string Schema = """
        CREATE TABLE IF NOT EXISTS assessments(operation_id TEXT PRIMARY KEY, service TEXT NOT NULL,
          decision TEXT NOT NULL, evidence_mode TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS outbox(event_id TEXT PRIMARY KEY, payload TEXT NOT NULL,
          sent INTEGER NOT NULL DEFAULT 0);
        """;
    public string DatabasePath { get; }
    public AssessmentStore(string path) { DatabasePath = path; Sql.Initialize(path, Schema); }
    public async Task<Assessment?> FindAsync(string operationId, CancellationToken ct)
    {
        using var db = Sql.Open(DatabasePath);
        using var command = Sql.Command(db, null, "SELECT * FROM assessments WHERE operation_id=$id", ("$id", operationId));
        using var row = await command.ExecuteReaderAsync(ct);
        return await row.ReadAsync(ct) ? new Assessment(row.GetString(0), row.GetString(1), row.GetString(2), row.GetString(3)) : null;
    }
    public async Task<Assessment> CommitAsync(Assessment assessment, RecordedEvent e, CancellationToken ct)
    {
        EventContract.Validate(e);
        try
        {
            using var db = Sql.Open(DatabasePath);
            using var transaction = db.BeginTransaction();
            using var writeAssessment = Sql.Command(db, transaction, "INSERT INTO assessments VALUES ($id,$service,$decision,$mode)",
                ("$id", assessment.OperationId), ("$service", assessment.Service),
                ("$decision", assessment.Decision), ("$mode", assessment.EvidenceMode));
            await writeAssessment.ExecuteNonQueryAsync(ct);
            using var writeEvent = Sql.Command(db, transaction, "INSERT INTO outbox(event_id,payload) VALUES ($id,$payload)",
                ("$id", e.EventId), ("$payload", EventContract.Serialize(e)));
            await writeEvent.ExecuteNonQueryAsync(ct);
            transaction.Commit();
            return assessment;
        }
        catch (SqliteException error) when (error.SqliteErrorCode == 19)
        {
            // A racing caller can commit while this caller retrieves evidence.
            var existing = await FindAsync(assessment.OperationId, ct);
            if (existing is null || existing.Service != assessment.Service) throw;
            return existing;
        }
    }
    public async Task<IReadOnlyList<PendingEvent>> PendingAsync(CancellationToken ct)
    {
        using var db = Sql.Open(DatabasePath);
        using var command = Sql.Command(db, null, "SELECT event_id,payload FROM outbox WHERE sent=0 ORDER BY rowid");
        using var rows = await command.ExecuteReaderAsync(ct);
        var result = new List<PendingEvent>();
        while (await rows.ReadAsync(ct)) result.Add(new(rows.GetString(0), rows.GetString(1)));
        return result;
    }
    public async Task<string> PayloadAsync(string operationId, CancellationToken ct)
    {
        using var db = Sql.Open(DatabasePath);
        using var command = Sql.Command(db, null, "SELECT payload FROM outbox ORDER BY rowid");
        using var rows = await command.ExecuteReaderAsync(ct);
        while (await rows.ReadAsync(ct))
        {
            string payload = rows.GetString(0);
            if (EventContract.Parse(payload).CorrelationId == operationId) return payload;
        }
        throw new KeyNotFoundException("The operation has no outbox event.");
    }
    public async Task MarkSentAsync(string eventId, CancellationToken ct)
    {
        using var db = Sql.Open(DatabasePath);
        using var command = Sql.Command(db, null, "UPDATE outbox SET sent=1 WHERE event_id=$id", ("$id", eventId));
        await command.ExecuteNonQueryAsync(ct);
    }
    public async Task<IReadOnlyList<Assessment>> AssessmentsAsync(CancellationToken ct)
    {
        using var db = Sql.Open(DatabasePath);
        using var command = Sql.Command(db, null, "SELECT * FROM assessments ORDER BY rowid");
        using var rows = await command.ExecuteReaderAsync(ct);
        var result = new List<Assessment>();
        while (await rows.ReadAsync(ct)) result.Add(new(rows.GetString(0), rows.GetString(1), rows.GetString(2), rows.GetString(3)));
        return result;
    }
}
