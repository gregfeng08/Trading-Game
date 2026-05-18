using Microsoft.Data.Sqlite;
using TradingGame.Data;
using TradingGame.Models;

namespace TradingGame.Services;

public class EntityService
{
    private readonly Database _db;

    public EntityService(Database db) => _db = db;

    public EntityRegistrationResponse Register(RegisterEntityRequest req)
    {
        var externalId = req.EntityId.Trim();
        var isPlayer = req.EntityType.Trim().Equals("player", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        var existing = ResolveExternalId(conn, externalId, tx);
        if (existing is not null)
        {
            tx.Commit();
            return new EntityRegistrationResponse("ok", "Entity already exists", existing.Value, true);
        }

        using var insert = conn.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = "INSERT INTO entity (is_player, available_cash) VALUES (@ip, @cash); SELECT last_insert_rowid();";
        insert.Parameters.AddWithValue("@ip", isPlayer);
        insert.Parameters.AddWithValue("@cash", req.StartingCash);
        var entityDbId = Convert.ToInt32(insert.ExecuteScalar()!);

        using var map = conn.CreateCommand();
        map.Transaction = tx;
        map.CommandText = "INSERT INTO entity_map (external_id, entity_db_id) VALUES (@eid, @dbid);";
        map.Parameters.AddWithValue("@eid", externalId);
        map.Parameters.AddWithValue("@dbid", entityDbId);
        map.ExecuteNonQuery();

        tx.Commit();
        return new EntityRegistrationResponse("ok", "Entity created", entityDbId, false);
    }

    public EntityCreateResponse Create(CreateEntityRequest req)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO entity (is_player, available_cash) VALUES (@ip, @cash); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@ip", req.IsPlayer);
        cmd.Parameters.AddWithValue("@cash", req.StartingCash);

        var entityId = Convert.ToInt32(cmd.ExecuteScalar()!);
        return new EntityCreateResponse("ok", entityId, req.IsPlayer, req.StartingCash);
    }

    public EntityInfoDto? GetEntity(int entityId)
    {
        using var conn = _db.Open();
        return GetEntity(conn, entityId);
    }

    public ResolveEntityResponse? Resolve(string externalId)
    {
        using var conn = _db.Open();
        var dbId = ResolveExternalId(conn, externalId);
        if (dbId is null) return null;
        return new ResolveEntityResponse("ok", externalId.Trim(), dbId.Value);
    }

    public int? ResolveExternalId(string externalId)
    {
        using var conn = _db.Open();
        return ResolveExternalId(conn, externalId);
    }

    // ── Internal helpers (used by TradingService too) ──

    public EntityInfoDto? GetEntity(SqliteConnection conn, int entityId, SqliteTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT entity_id, is_player, available_cash FROM entity WHERE entity_id = @eid;";
        cmd.Parameters.AddWithValue("@eid", entityId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new EntityInfoDto(reader.GetInt32(0), reader.GetInt32(1), reader.GetDouble(2));
    }

    public int? ResolveExternalId(SqliteConnection conn, string externalId, SqliteTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT entity_db_id FROM entity_map WHERE external_id = @eid;";
        cmd.Parameters.AddWithValue("@eid", externalId.Trim());

        var result = cmd.ExecuteScalar();
        return result is not null ? Convert.ToInt32(result) : null;
    }

    public void UpdateCash(SqliteConnection conn, SqliteTransaction tx, int entityId, double newCash)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE entity SET available_cash = @cash WHERE entity_id = @eid;";
        cmd.Parameters.AddWithValue("@cash", newCash);
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.ExecuteNonQuery();
    }
}
