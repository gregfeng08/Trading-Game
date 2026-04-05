using Microsoft.Data.Sqlite;
using TradingGame.Data;

namespace TradingGame.Endpoints;

public static class GameEndpoints
{
    public static void Map(WebApplication app)
    {
        // ---- Game date management ----

        // Returns the current game date, or null if no game is active.
        app.MapGet("/get_game_date", (Database db) =>
        {
            using var conn = db.Open();
            var date = GetSaveValue(conn, "current_date");

            if (date is null)
                return Results.Ok(new { status = "ok", current_date = (string?)null, message = "No active game. Call /new_game to start." });

            return Results.Ok(new { status = "ok", current_date = date });
        });

        // Advances the game date to the next trading day (i.e. the next date that has price data).
        app.MapPost("/advance_day", (Database db) =>
        {
            using var conn = db.Open();
            var currentDate = GetSaveValue(conn, "current_date");

            if (currentDate is null)
                return Results.Json(new { status = "error", message = "No active game. Call /new_game first." }, statusCode: 400);

            // Find the next trading day after current_date
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT MIN(date) FROM ticker_prices WHERE date > @d;
                """;
            cmd.Parameters.AddWithValue("@d", currentDate);
            var next = cmd.ExecuteScalar();

            if (next is null or DBNull)
                return Results.Ok(new { status = "ok", message = "No more trading days. Game complete.", current_date = currentDate, game_over = true });

            var nextDate = (string)next;
            SetSaveValue(conn, "current_date", nextDate);

            return Results.Ok(new
            {
                status = "ok",
                previous_date = currentDate,
                current_date = nextDate,
                game_over = false,
            });
        });

        // ---- New game / reset ----

        // Resets all player state but keeps loaded ticker data intact.
        app.MapPost("/new_game", (string? startDate, Database db) =>
        {
            using var conn = db.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                // Clear game state tables (not ticker data)
                // Delete child tables first to satisfy FK constraints (entity_map/portfolio/trade_history depend on entity)
                foreach (var table in new[] { "entity_map", "portfolio", "trade_history", "entity", "save_state", "static_npc_dialogue", "dynamic_npc_dialogue" })
                {
                    using var del = conn.CreateCommand();
                    del.Transaction = tx;
                    del.CommandText = $"DELETE FROM \"{table}\";";
                    del.ExecuteNonQuery();
                }

                // Determine start date: use provided, or first available trading day
                string gameStartDate;
                if (startDate is not null)
                {
                    gameStartDate = startDate;
                }
                else
                {
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "SELECT MIN(date) FROM ticker_prices;";
                    var min = cmd.ExecuteScalar();
                    if (min is null or DBNull)
                    {
                        tx.Rollback();
                        return Results.Json(new { status = "error", message = "No ticker data loaded. Run ticker_download.py first." }, statusCode: 400);
                    }
                    gameStartDate = (string)min;
                }

                // Set current_date
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "INSERT INTO save_state (key, value) VALUES ('current_date', @d);";
                    cmd.Parameters.AddWithValue("@d", gameStartDate);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                return Results.Ok(new
                {
                    status = "ok",
                    message = "New game started",
                    current_date = gameStartDate,
                });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return Results.Json(new { status = "error", message = ex.Message }, statusCode: 500);
            }
        });

        // ---- NPC dialogue ----

        // GET /dialogue?date=2010-01-04&npc_type=analyst&ticker_id=AAPL
        // All params optional. Returns matching dialogue rows.
        app.MapGet("/dialogue", (string? date, string? npcType, string? tickerId, string? category, Database db) =>
        {
            using var conn = db.Open();

            // If no date provided, use current game date
            date ??= GetSaveValue(conn, "current_date");

            using var cmd = conn.CreateCommand();
            var clauses = new List<string>();

            if (date is not null)
            {
                clauses.Add("date = @d");
                cmd.Parameters.AddWithValue("@d", date);
            }
            if (npcType is not null)
            {
                clauses.Add("npc_type = @npc");
                cmd.Parameters.AddWithValue("@npc", npcType.Trim().ToLowerInvariant());
            }
            if (tickerId is not null)
            {
                clauses.Add("ticker_id = @tid");
                cmd.Parameters.AddWithValue("@tid", tickerId.ToUpperInvariant().Trim());
            }
            if (category is not null)
            {
                clauses.Add("category = @cat");
                cmd.Parameters.AddWithValue("@cat", category.Trim().ToLowerInvariant());
            }

            var where = clauses.Count > 0 ? "WHERE " + string.Join(" AND ", clauses) : "";

            // Query both static and dynamic tables, union them
            cmd.CommandText = $"""
                SELECT id, date, ticker_id, npc_type, category, text, 'static' AS source
                FROM static_npc_dialogue {where}
                UNION ALL
                SELECT id, date, ticker_id, npc_type, category, text, 'dynamic' AS source
                FROM dynamic_npc_dialogue {where}
                ORDER BY date, npc_type, category;
                """;

            var rows = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new
                {
                    id = reader.GetInt32(0),
                    date = reader.IsDBNull(1) ? null : reader.GetString(1),
                    ticker_id = reader.IsDBNull(2) ? null : reader.GetString(2),
                    npc_type = reader.IsDBNull(3) ? null : reader.GetString(3),
                    category = reader.IsDBNull(4) ? null : reader.GetString(4),
                    text = reader.IsDBNull(5) ? null : reader.GetString(5),
                    source = reader.GetString(6),
                });
            }

            return Results.Ok(new { status = "ok", date, count = rows.Count, dialogue = rows });
        });
    }

    // ---- Helpers ----

    public static string? GetSaveValue(SqliteConnection conn, string key, SqliteTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT value FROM save_state WHERE key = @k;";
        cmd.Parameters.AddWithValue("@k", key);
        var result = cmd.ExecuteScalar();
        return result is not null and not DBNull ? (string)result : null;
    }

    public static void SetSaveValue(SqliteConnection conn, string key, string value, SqliteTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO save_state (key, value) VALUES (@k, @v)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("@k", key);
        cmd.Parameters.AddWithValue("@v", value);
        cmd.ExecuteNonQuery();
    }
}
