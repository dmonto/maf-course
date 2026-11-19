using Microsoft.Data.Sqlite;

internal sealed class TicketStore(string file)
{
    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = file, DefaultTimeout = 5 }.ToString());
        connection.Open();
        return connection;
    }

    public void Initialize()
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS tickets(
                id TEXT PRIMARY KEY, tenant TEXT NOT NULL,
                service TEXT NOT NULL, status TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS deliveries(
                operation_key TEXT PRIMARY KEY, payload_hash TEXT NOT NULL,
                state TEXT NOT NULL, receipt TEXT);
            INSERT OR IGNORE INTO tickets(id,tenant,service,status) VALUES
                ('A-101','training-a','vpn','open'),
                ('A-102','training-a','vpn','open'),
                ('A-103','training-a','vpn','closed'),
                ('A-104','training-a','erp','open'),
                ('A-105','training-a','payroll','open'),
                ('B-101','training-b','vpn','open');
            """;
        cmd.ExecuteNonQuery();
    }

    public int CountOpen(Caller caller, string service)
    {
        caller.Demand(service);
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM tickets
            WHERE tenant = $tenant AND service = $service AND status = 'open';
            """;
        cmd.Parameters.AddWithValue("$tenant", caller.Tenant);
        cmd.Parameters.AddWithValue("$service", service);
        return checked((int)(long)cmd.ExecuteScalar()!);
    }

    // A unique key prevents two local processes from claiming the same operation.
    public bool TryClaim(string key, string hash)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO deliveries(operation_key,payload_hash,state)
            VALUES($key,$hash,'sending');
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$hash", hash);
        return cmd.ExecuteNonQuery() == 1;
    }

    public string State(string key)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT state FROM deliveries WHERE operation_key=$key;";
        cmd.Parameters.AddWithValue("$key", key);
        return cmd.ExecuteScalar()?.ToString() ?? "absent";
    }

    public void Complete(string key, string state, string? receipt)
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            UPDATE deliveries SET state=$state,receipt=$receipt WHERE operation_key=$key;
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$state", state);
        cmd.Parameters.AddWithValue("$receipt", (object?)receipt ?? DBNull.Value);
        if (cmd.ExecuteNonQuery() != 1) throw new InvalidOperationException("Missing delivery claim.");
    }
}
