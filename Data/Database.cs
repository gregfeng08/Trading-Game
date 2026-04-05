using Microsoft.Data.Sqlite;

namespace TradingGame.Data;

public class Database
{
    private readonly string _connectionString;
    private readonly string _schemaPath;

    public Database(string dbPath, string schemaPath)
    {
        _connectionString = $"Data Source={dbPath}";
        _schemaPath = schemaPath;
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();

        // Enable WAL mode + foreign keys on every connection
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();

        return conn;
    }

    public void InitSchema()
    {
        var sql = File.ReadAllText(_schemaPath);
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void DropAllTables()
    {
        using var conn = Open();

        // Collect table names first
        var tables = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                tables.Add(reader.GetString(0));
        }

        foreach (var table in tables)
        {
            using var drop = conn.CreateCommand();
            drop.CommandText = $"DROP TABLE IF EXISTS \"{table.Replace("\"", "\"\"")}\"";
            drop.ExecuteNonQuery();
        }
    }
}
