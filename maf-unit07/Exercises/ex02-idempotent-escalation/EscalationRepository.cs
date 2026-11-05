using Microsoft.Data.Sqlite;
using Unit07.Common;

public sealed class EscalationRepository
{
    private readonly string _connectionString;
    public EscalationRepository(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path, DefaultTimeout = 5 }.ToString();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS escalations (
              operation_key TEXT PRIMARY KEY, escalation_id TEXT NOT NULL UNIQUE,
              case_id TEXT NOT NULL, status TEXT NOT NULL, draft TEXT NOT NULL,
              created_utc TEXT NOT NULL);
            """;
        command.ExecuteNonQuery();
    }
    public EscalationOutcome GetOrCreate(Proposal proposal)
    {
        string caseId = proposal.Evidence.Request.CaseId;
        string operationKey = caseId + ":escalate:v1";
        string proposedId = "ESC-" + Guid.NewGuid().ToString("N")[..12];
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO escalations(operation_key, escalation_id, case_id, status, draft, created_utc)
            VALUES ($key, $id, $case, $status, $draft, $utc)
            ON CONFLICT(operation_key) DO NOTHING;
            """;
        insert.Parameters.AddWithValue("$key", operationKey);
        insert.Parameters.AddWithValue("$id", proposedId);
        insert.Parameters.AddWithValue("$case", caseId);
        insert.Parameters.AddWithValue("$status", proposal.Evidence.Status);
        insert.Parameters.AddWithValue("$draft", proposal.Draft);
        insert.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
        bool created = insert.ExecuteNonQuery() == 1;
        using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = "SELECT escalation_id FROM escalations WHERE operation_key=$key";
        select.Parameters.AddWithValue("$key", operationKey);
        string id = (string?)select.ExecuteScalar() ?? throw new InvalidOperationException("missing-operation-receipt");
        transaction.Commit();
        return new(caseId, "escalation-created", proposal.Evidence.Status, id, operationKey, !created);
    }
    public long Count()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM escalations";
        return (long)command.ExecuteScalar()!;
    }
    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }
}
