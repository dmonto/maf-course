using Microsoft.Data.Sqlite;

namespace Maf.Unit15.Infrastructure;

public static class Sql
{
    public static SqliteConnection Open(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = path, DefaultTimeout = 10, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }
    public static SqliteCommand Command(SqliteConnection db, SqliteTransaction? transaction,
        string text, params (string Name, object? Value)[] parameters)
    {
        var command = db.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = text;
        foreach (var p in parameters) command.Parameters.AddWithValue(p.Name, p.Value ?? DBNull.Value);
        return command;
    }
    public static void Initialize(string path, string schema)
    {
        using var db = Open(path);
        using var command = Command(db, null, schema);
        command.ExecuteNonQuery();
    }
}
