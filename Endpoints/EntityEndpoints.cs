using Microsoft.Data.Sqlite;
using TradingGame.Data;
using TradingGame.Models;

namespace TradingGame.Endpoints;

public static class EntityEndpoints
{
    public static void Map(WebApplication app)
    {
        // Unity expects: { status, message, entity_db_id, already_exists }
        app.MapPost("/register_entity", (RegisterEntityRequest req, Database db) =>
        {
            var externalId = req.EntityId.Trim();
            var isPlayer = req.EntityType.Trim().Equals("player", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            using var conn = db.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                // Check if already registered
                using (var check = conn.CreateCommand())
                {
                    check.Transaction = tx;
                    check.CommandText = "SELECT entity_db_id FROM entity_map WHERE external_id = @eid;";
                    check.Parameters.AddWithValue("@eid", externalId);

                    var existing = check.ExecuteScalar();
                    if (existing is not null)
                    {
                        tx.Commit();
                        return Results.Ok(new
                        {
                            status = "ok",
                            message = "Entity already exists",
                            entity_db_id = Convert.ToInt32(existing),
                            already_exists = true,
                        });
                    }
                }

                // Insert entity
                int entityDbId;
                using (var insert = conn.CreateCommand())
                {
                    insert.Transaction = tx;
                    insert.CommandText = "INSERT INTO entity (is_player, available_cash) VALUES (@ip, @cash); SELECT last_insert_rowid();";
                    insert.Parameters.AddWithValue("@ip", isPlayer);
                    insert.Parameters.AddWithValue("@cash", req.StartingCash);
                    entityDbId = Convert.ToInt32(insert.ExecuteScalar()!);
                }

                // Insert mapping
                using (var map = conn.CreateCommand())
                {
                    map.Transaction = tx;
                    map.CommandText = "INSERT INTO entity_map (external_id, entity_db_id) VALUES (@eid, @dbid);";
                    map.Parameters.AddWithValue("@eid", externalId);
                    map.Parameters.AddWithValue("@dbid", entityDbId);
                    map.ExecuteNonQuery();
                }

                tx.Commit();
                return Results.Ok(new
                {
                    status = "ok",
                    message = "Entity created",
                    entity_db_id = entityDbId,
                    already_exists = false,
                });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return Results.Json(new { status = "error", message = ex.Message }, statusCode: 500);
            }
        });

        app.MapPost("/entity_create", (CreateEntityRequest req, Database db) =>
        {
            using var conn = db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO entity (is_player, available_cash) VALUES (@ip, @cash); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@ip", req.IsPlayer);
            cmd.Parameters.AddWithValue("@cash", req.StartingCash);

            var entityId = Convert.ToInt32(cmd.ExecuteScalar()!);
            return Results.Ok(new
            {
                status = "ok",
                entity_id = entityId,
                is_player = req.IsPlayer,
                available_cash = req.StartingCash,
            });
        });

        app.MapGet("/entity", (int entityId, Database db) =>
        {
            using var conn = db.Open();
            var entity = GetEntityDict(conn, entityId);
            if (entity is null)
                return Results.Json(new { status = "error", message = "Entity not found" }, statusCode: 404);

            return Results.Ok(new { status = "ok", entity });
        });

        app.MapGet("/resolve_entity", (string externalId, Database db) =>
        {
            using var conn = db.Open();
            var dbId = ResolveExternalId(conn, externalId);
            if (dbId is null)
                return Results.Json(new { status = "error", message = $"External ID '{externalId}' not found" }, statusCode: 404);

            return Results.Ok(new { status = "ok", external_id = externalId.Trim(), entity_db_id = dbId.Value });
        });
    }

    /// <summary>
    /// Looks up an entity by internal DB id. Returns null if not found.
    /// </summary>
    public static Dictionary<string, object>? GetEntityDict(SqliteConnection conn, int entityId, SqliteTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT entity_id, is_player, available_cash FROM entity WHERE entity_id = @eid;";
        cmd.Parameters.AddWithValue("@eid", entityId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new Dictionary<string, object>
        {
            ["entity_id"] = reader.GetInt32(0),
            ["is_player"] = reader.GetInt32(1),
            ["available_cash"] = reader.GetDouble(2),
        };
    }

    /// <summary>
    /// Resolves an external entity ID (e.g. "player_001") to the internal DB id.
    /// Returns null if not found.
    /// </summary>
    public static int? ResolveExternalId(SqliteConnection conn, string externalId, SqliteTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT entity_db_id FROM entity_map WHERE external_id = @eid;";
        cmd.Parameters.AddWithValue("@eid", externalId.Trim());

        var result = cmd.ExecuteScalar();
        return result is not null ? Convert.ToInt32(result) : null;
    }
}
