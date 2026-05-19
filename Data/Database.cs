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

        MigrateNetWorthHistory(conn);

        var seedPath = Path.Combine(Path.GetDirectoryName(_schemaPath)!, "seed_static_dialogue.sql");
        if (File.Exists(seedPath))
        {
            using var countCmd = conn.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM static_npc_dialogue WHERE date IS NULL;";
            var existing = Convert.ToInt32(countCmd.ExecuteScalar());
            if (existing == 0)
            {
                var seedSql = File.ReadAllText(seedPath);
                using var seedCmd = conn.CreateCommand();
                seedCmd.CommandText = seedSql;
                seedCmd.ExecuteNonQuery();
            }
        }
    }

    private static void MigrateNetWorthHistory(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "PRAGMA table_info(net_worth_history);";
        bool hasPhase = false;
        using (var reader = check.ExecuteReader())
        {
            while (reader.Read())
            {
                if (reader.GetString(1) == "phase") { hasPhase = true; break; }
            }
        }

        if (hasPhase) return;

        using var migrate = conn.CreateCommand();
        migrate.CommandText = """
            ALTER TABLE net_worth_history RENAME TO net_worth_history_old;
            CREATE TABLE net_worth_history (
                entity_id INTEGER NOT NULL,
                date TEXT NOT NULL,
                phase TEXT NOT NULL DEFAULT 'close',
                cash REAL NOT NULL,
                holdings_value REAL NOT NULL,
                net_worth REAL NOT NULL,
                PRIMARY KEY (entity_id, date, phase),
                FOREIGN KEY (entity_id) REFERENCES entity(entity_id)
            );
            INSERT INTO net_worth_history (entity_id, date, phase, cash, holdings_value, net_worth)
            SELECT entity_id, date, 'close', cash, holdings_value, net_worth FROM net_worth_history_old;
            DROP TABLE net_worth_history_old;
            """;
        migrate.ExecuteNonQuery();
    }

    public void DropAllTables()
    {
        using var conn = Open();

        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = OFF;";
            pragma.ExecuteNonQuery();
        }

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

        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }
    }
}
