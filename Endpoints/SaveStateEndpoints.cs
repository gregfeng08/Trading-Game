using TradingGame.Data;

namespace TradingGame.Endpoints;

public static class SaveStateEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/save_state", (TradingGame.Models.SaveStateRequest req, Database db) =>
        {
            using var conn = db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO save_state (key, value)
                VALUES (@k, @v)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """;
            cmd.Parameters.AddWithValue("@k", req.Key);
            cmd.Parameters.AddWithValue("@v", req.Value);
            cmd.ExecuteNonQuery();

            return Results.Ok(new { status = "ok", key = req.Key, value = req.Value });
        });

        app.MapGet("/save_state", (string key, Database db) =>
        {
            using var conn = db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT key, value FROM save_state WHERE key = @k;";
            cmd.Parameters.AddWithValue("@k", key);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return Results.Json(new { status = "error", message = "Key not found" }, statusCode: 404);

            return Results.Ok(new
            {
                status = "ok",
                key = reader.GetString(0),
                value = reader.GetString(1),
            });
        });
    }
}
