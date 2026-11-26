using System.Text.Json;
using Maf.Unit15.Application;
using System.Text.Json.Nodes;

namespace Maf.Unit15.Infrastructure;

public sealed record TransitionReceipt(string Outcome, string From, string To);
public sealed record ProcessState(string Id, string Service, string State);
public sealed record HistoryItem(string State, string Actor, string OccurredAt);
public sealed record ProcessView(ProcessState Process, IReadOnlyList<HistoryItem> History);
public sealed class TransitionRejected(string message) : InvalidOperationException(message);

public sealed class RepairProcess
{
    private readonly string path;
    public RepairProcess(string path)
    {
        this.path = path;
        Sql.Initialize(path, """
            CREATE TABLE IF NOT EXISTS processes(id TEXT PRIMARY KEY,service TEXT NOT NULL,state TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS commands(id TEXT PRIMARY KEY,payload TEXT NOT NULL,result TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS history(sequence INTEGER PRIMARY KEY AUTOINCREMENT,process_id TEXT NOT NULL,
              state TEXT NOT NULL,actor TEXT NOT NULL,occurred_at TEXT NOT NULL);
            """);
    }
    public void Open(string id, string service)
    {
        if (service is not ("vpn" or "email")) throw new TransitionRejected("Unsupported service.");
        using var db = Sql.Open(path);
        using var tx = db.BeginTransaction();
        using var find = Sql.Command(db, tx, "SELECT service FROM processes WHERE id=$id", ("$id", id));
        string? previous = find.ExecuteScalar() as string;
        if (previous is not null)
        {
            if (previous != service) throw new TransitionRejected("Process identity conflict.");
            return;
        }
        using var insert = Sql.Command(db, tx, "INSERT INTO processes VALUES ($id,$service,'AwaitingApproval')", ("$id", id), ("$service", service));
        insert.ExecuteNonQuery();
        History(db, tx, id, "AwaitingApproval", "application-fixture");
        tx.Commit();
        LabConsole.Write("PROCESS", $"Opened repair process {id} in state AwaitingApproval (no repair executed).");
    }
    public TransitionReceipt Apply(string id, string commandId, string action, string actor)
    {
        string payload = Application.EventContract.Serialize(new { processId = id, action, actor });
        using var db = Sql.Open(path);
        using var tx = db.BeginTransaction();
        using var duplicate = Sql.Command(db, tx, "SELECT payload,result FROM commands WHERE id=$id", ("$id", commandId));
        using (var row = duplicate.ExecuteReader())
        {
            if (row.Read())
            {
                if (!JsonNode.DeepEquals(JsonNode.Parse(row.GetString(0)), JsonNode.Parse(payload)))
                    throw new TransitionRejected("Command ID reused with different content.");
                var receipt = JsonSerializer.Deserialize<TransitionReceipt>(row.GetString(1), Application.EventContract.Json)!;
                LabConsole.Write("PROCESS", $"Command {commandId} was already applied => duplicate; state unchanged.");
                return receipt with { Outcome = "duplicate" };
            }
        }
        using var current = Sql.Command(db, tx, "SELECT state FROM processes WHERE id=$id", ("$id", id));
        string? state = current.ExecuteScalar() as string;
        var transition = (action, state) switch
        {
            ("approve", "AwaitingApproval") => ("Authorized", "reviewer-demo"),
            ("request-repair", "Authorized") => ("RepairRequested", "operator-demo"),
            ("verify-success", "RepairRequested") => ("Verified", "verifier-demo"),
            ("verify-failure", "RepairRequested") => ("NeedsManualRecovery", "verifier-demo"),
            _ => throw new TransitionRejected("The transition is not allowed from the current state.")
        };
        // Fixed synthetic identities model ownership; they do not authenticate users.
        if (actor != transition.Item2) throw new TransitionRejected("The synthetic actor does not own this transition.");
        using var update = Sql.Command(db, tx, "UPDATE processes SET state=$next WHERE id=$id AND state=$old",
            ("$next", transition.Item1), ("$id", id), ("$old", state));
        if (update.ExecuteNonQuery() != 1) throw new TransitionRejected("Concurrent transition; reload the process.");
        var result = new TransitionReceipt("applied", state!, transition.Item1);
        History(db, tx, id, result.To, actor);
        using var save = Sql.Command(db, tx, "INSERT INTO commands VALUES ($id,$payload,$result)",
            ("$id", commandId), ("$payload", payload), ("$result", Application.EventContract.Serialize(result)));
        save.ExecuteNonQuery();
        tx.Commit();
        LabConsole.Write("PROCESS", $"{id}: '{action}' by {actor}: {result.From} -> {result.To}");
        return result;
    }
    private static void History(Microsoft.Data.Sqlite.SqliteConnection db, Microsoft.Data.Sqlite.SqliteTransaction tx,
        string id, string state, string actor)
    {
        using var command = Sql.Command(db, tx, "INSERT INTO history(process_id,state,actor,occurred_at) VALUES ($id,$state,$actor,$at)",
            ("$id", id), ("$state", state), ("$actor", actor), ("$at", DateTimeOffset.UtcNow.ToString("O")));
        command.ExecuteNonQuery();
    }
    public ProcessView Inspect(string id)
    {
        using var db = Sql.Open(path);
        using var current = Sql.Command(db, null, "SELECT * FROM processes WHERE id=$id", ("$id", id));
        ProcessState process;
        using (var row = current.ExecuteReader()) { if (!row.Read()) throw new KeyNotFoundException(); process = new(row.GetString(0), row.GetString(1), row.GetString(2)); }
        using var history = Sql.Command(db, null, "SELECT state,actor,occurred_at FROM history WHERE process_id=$id ORDER BY sequence", ("$id", id));
        using var records = history.ExecuteReader();
        var items = new List<HistoryItem>();
        while (records.Read()) items.Add(new(records.GetString(0), records.GetString(1), records.GetString(2)));
        return new(process, items);
    }
}
