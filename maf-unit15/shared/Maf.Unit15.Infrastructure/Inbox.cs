using Maf.Unit15.Application;
using Microsoft.Data.Sqlite;

namespace Maf.Unit15.Infrastructure;

public sealed class Inbox
{
    public string DatabasePath { get; }
    public Inbox(string path, string businessSchema)
    {
        DatabasePath = path;
        Sql.Initialize(path, "CREATE TABLE IF NOT EXISTS inbox(event_id TEXT PRIMARY KEY,payload TEXT NOT NULL);" + businessSchema);
    }
    public async Task<string> ConsumeAsync(string payload,
        Action<SqliteConnection, SqliteTransaction, RecordedEvent> apply, CancellationToken ct)
    {
        RecordedEvent e = EventContract.Parse(payload);
        LabConsole.Write("INBOX", $"Consumer transaction: INSERT OR IGNORE event {e.EventId} into inbox table.");
        using var db = Sql.Open(DatabasePath);
        using var transaction = db.BeginTransaction();
        using var insert = Sql.Command(db, transaction, "INSERT OR IGNORE INTO inbox VALUES ($id,$payload)",
            ("$id", e.EventId), ("$payload", payload));
        int added = await insert.ExecuteNonQueryAsync(ct);
        if (added == 0)
        {
            using var read = Sql.Command(db, transaction, "SELECT payload FROM inbox WHERE event_id=$id", ("$id", e.EventId));
            if ((string?)await read.ExecuteScalarAsync(ct) != payload)
                throw new InvalidDataException("Event ID reused with different content.");
            transaction.Commit();
            LabConsole.Write("INBOX", "Event ID already in the inbox with identical content => duplicate; business effect NOT repeated.");
            return "duplicate";
        }
        // The callback only writes this consumer's business state in this transaction.
        apply(db, transaction, e);
        transaction.Commit();
        LabConsole.Write("INBOX", $"New event => inbox row + business effect committed atomically (decision={e.Data.Decision}).");
        return "applied";
    }
    public async Task<long> CountAsync(string table, CancellationToken ct)
    {
        if (table is not ("inbox" or "review_items" or "audit_records" or "quarantine"))
            throw new ArgumentException("Unknown counter table.");
        using var db = Sql.Open(DatabasePath);
        using var command = Sql.Command(db, null, $"SELECT count(*) FROM {table}");
        return (long)(await command.ExecuteScalarAsync(ct))!;
    }
}

public sealed class ReviewConsumer : IEventConsumer
{
    public const string Schema = "CREATE TABLE IF NOT EXISTS review_items(operation_id TEXT PRIMARY KEY,service TEXT NOT NULL,state TEXT NOT NULL);";
    public Inbox Inbox { get; }
    public ReviewConsumer(string path) => Inbox = new(path, Schema);
    public Task<string> ConsumeAsync(string payload, CancellationToken ct) => Inbox.ConsumeAsync(payload, (db, tx, e) =>
    {
        if (e.Data.Decision == "review_required")
        {
            using var command = Sql.Command(db, tx, "INSERT INTO review_items VALUES ($id,$service,'pending')",
                ("$id", e.Data.OperationId), ("$service", e.Data.Service));
            command.ExecuteNonQuery();
        }
    }, ct);
    public async Task<IReadOnlyList<ReviewItem>> ItemsAsync(CancellationToken ct)
    {
        using var db = Sql.Open(Inbox.DatabasePath);
        using var command = Sql.Command(db, null, "SELECT * FROM review_items ORDER BY rowid");
        using var rows = await command.ExecuteReaderAsync(ct);
        var result = new List<ReviewItem>();
        while (await rows.ReadAsync(ct)) result.Add(new(rows.GetString(0), rows.GetString(1), rows.GetString(2)));
        return result;
    }
}

public sealed class AuditConsumer : IEventConsumer
{
    public Inbox Inbox { get; }
    public AuditConsumer(string path) => Inbox = new(path,
        "CREATE TABLE IF NOT EXISTS audit_records(event_id TEXT PRIMARY KEY,operation_id TEXT NOT NULL,service TEXT NOT NULL);");
    public Task<string> ConsumeAsync(string payload, CancellationToken ct) => Inbox.ConsumeAsync(payload, (db, tx, e) =>
    {
        using var command = Sql.Command(db, tx, "INSERT INTO audit_records VALUES ($id,$operation,$service)",
            ("$id", e.EventId), ("$operation", e.CorrelationId), ("$service", e.Data.Service));
        command.ExecuteNonQuery();
    }, ct);
}
