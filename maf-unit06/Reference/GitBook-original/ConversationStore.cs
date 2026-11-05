using System.Text.Json;
using Microsoft.Data.Sqlite;

public sealed class ConversationStore
{
    private readonly string _connectionString;

    public ConversationStore(string path)
    {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path, DefaultTimeout = 5
        }.ToString();
        using var connection = Open();
        using var version = connection.CreateCommand();
        version.CommandText = "PRAGMA user_version";
        long schemaVersion = (long)version.ExecuteScalar()!;
        if (schemaVersion is not (0 or 1)) throw new MemoryFault("schema-mismatch");
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS conversations (
              tenant TEXT NOT NULL, owner TEXT NOT NULL, session_id TEXT NOT NULL,
              agent_config TEXT NOT NULL, revision INTEGER NOT NULL,
              business_json TEXT NOT NULL, session_json TEXT NOT NULL,
              expires_unix INTEGER NOT NULL,
              PRIMARY KEY (tenant, owner, session_id));
            CREATE TABLE IF NOT EXISTS turn_audit (
              run_id TEXT PRIMARY KEY, tenant TEXT NOT NULL, owner TEXT NOT NULL,
              session_id TEXT NOT NULL, revision INTEGER NOT NULL,
              committed_utc TEXT NOT NULL, input_chars INTEGER NOT NULL,
              output_chars INTEGER NOT NULL, dropped_turns INTEGER NOT NULL,
              elapsed_ms INTEGER NOT NULL);
            PRAGMA user_version = 1;
            """;
        command.ExecuteNonQuery();
    }

    public StoredConversation? Load(ActorContext actor, string sessionId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT revision, agent_config, business_json, session_json, expires_unix
            FROM conversations
            WHERE tenant=$tenant AND owner=$owner AND session_id=$session;
            """;
        AddScope(command, actor, sessionId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        long expires = reader.GetInt64(4);
        if (expires <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            throw new MemoryFault("memory-expired");
        string payload = reader.GetString(3);
        if (payload.Length > 128_000) throw new MemoryFault("snapshot-too-large");
        using var json = JsonDocument.Parse(payload);
        var state = JsonSerializer.Deserialize<CaseState>(reader.GetString(2))
            ?? throw new MemoryFault("invalid-state");
        return new(reader.GetInt64(0), reader.GetString(1), state,
            json.RootElement.Clone(), expires);
    }

    public void Save(ActorContext actor, string sessionId, long expectedRevision,
        string agentConfig, CaseState state, JsonElement session, TurnAudit audit)
    {
        string payload = session.GetRawText();
        if (payload.Length > 128_000) throw new MemoryFault("snapshot-too-large");
        if (audit.Revision != expectedRevision + 1)
            throw new MemoryFault("invalid-revision");
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = expectedRevision == 0 ? """
            INSERT OR IGNORE INTO conversations
              (tenant,owner,session_id,agent_config,revision,business_json,
               session_json,expires_unix)
            VALUES ($tenant,$owner,$session,$config,1,$state,$payload,$expires);
            """ : """
            UPDATE conversations SET agent_config=$config, revision=revision+1,
              business_json=$state, session_json=$payload, expires_unix=$expires
            WHERE tenant=$tenant AND owner=$owner AND session_id=$session
              AND revision=$expected;
            """;
        AddScope(command, actor, sessionId);
        command.Parameters.AddWithValue("$config", agentConfig);
        command.Parameters.AddWithValue("$state", JsonSerializer.Serialize(state));
        command.Parameters.AddWithValue("$payload", payload);
        command.Parameters.AddWithValue("$expires",
            DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds());
        if (expectedRevision != 0)
            command.Parameters.AddWithValue("$expected", expectedRevision);
        if (command.ExecuteNonQuery() != 1) throw new MemoryFault("version-conflict");

        using var log = connection.CreateCommand();
        log.Transaction = transaction;
        log.CommandText = """
            INSERT INTO turn_audit
              (run_id,tenant,owner,session_id,revision,committed_utc,
               input_chars,output_chars,dropped_turns,elapsed_ms)
            VALUES ($run,$tenant,$owner,$session,$revision,$utc,
               $input,$output,$dropped,$elapsed);
            """;
        AddScope(log, actor, sessionId);
        log.Parameters.AddWithValue("$run", audit.RunId);
        log.Parameters.AddWithValue("$revision", audit.Revision);
        log.Parameters.AddWithValue("$utc", audit.CommittedUtc);
        log.Parameters.AddWithValue("$input", audit.InputChars);
        log.Parameters.AddWithValue("$output", audit.OutputChars);
        log.Parameters.AddWithValue("$dropped", audit.DroppedTurns);
        log.Parameters.AddWithValue("$elapsed", audit.ElapsedMs);
        log.ExecuteNonQuery();
        transaction.Commit();
    }

    public List<TurnAudit> ReadAudit(ActorContext actor, string sessionId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT run_id,revision,committed_utc,input_chars,output_chars,
              dropped_turns,elapsed_ms FROM turn_audit
            WHERE tenant=$tenant AND owner=$owner AND session_id=$session
            ORDER BY revision DESC LIMIT 10;
            """;
        AddScope(command, actor, sessionId);
        using var reader = command.ExecuteReader();
        var rows = new List<TurnAudit>();
        while (reader.Read()) rows.Add(new(reader.GetString(0), reader.GetInt64(1),
            reader.GetString(2), reader.GetInt32(3), reader.GetInt32(4),
            reader.GetInt32(5), reader.GetInt64(6)));
        return rows;
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    private static void AddScope(SqliteCommand command, ActorContext actor, string id)
    {
        command.Parameters.AddWithValue("$tenant", actor.TenantId);
        command.Parameters.AddWithValue("$owner", actor.UserId);
        command.Parameters.AddWithValue("$session", id);
    }
}
