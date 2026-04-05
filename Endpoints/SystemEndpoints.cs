using TradingGame.Data;

namespace TradingGame.Endpoints;

public static class SystemEndpoints
{
    private static readonly DateTimeOffset StartTime = DateTimeOffset.UtcNow;

    public static void Map(WebApplication app)
    {
        app.MapGet("/", () => Results.Ok(new { message = "Trading Game API" }));

        // Unity expects: { status: "ok", server_time: <epoch_seconds> }
        app.MapGet("/ping", () => Results.Ok(new
        {
            status = "ok",
            server_time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
        }));

        // Unity expects: { status, server_time, uptime_seconds, db_connected, version }
        app.MapGet("/status", (Database db) =>
        {
            bool dbOk = true;
            try
            {
                using var conn = db.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1;";
                cmd.ExecuteScalar();
            }
            catch
            {
                dbOk = false;
            }

            return Results.Ok(new
            {
                status = "ok",
                server_time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
                uptime_seconds = (DateTimeOffset.UtcNow - StartTime).TotalSeconds,
                db_connected = dbOk,
                version = "dotnet-dev",
            });
        });

        // Unity expects: { status, message, initialized }
        app.MapPost("/init_db", (Database db) =>
        {
            try
            {
                db.InitSchema();
                return Results.Ok(new
                {
                    status = "ok",
                    message = "Database initialized",
                    initialized = true,
                });
            }
            catch (Exception ex)
            {
                return Results.Json(new
                {
                    status = "error",
                    message = ex.Message,
                    initialized = false,
                }, statusCode: 500);
            }
        });

        // Unity expects: { status, message, ticker_count }
        // Data is pre-loaded via Python script; this endpoint reports what's available.
        app.MapPost("/load_tickers", (Database db) =>
        {
            try
            {
                using var conn = db.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM loaded_ticker_list;";
                var count = Convert.ToInt32(cmd.ExecuteScalar());

                return Results.Ok(new
                {
                    status = "ok",
                    message = count > 0
                        ? $"{count} tickers available (pre-loaded)"
                        : "No tickers loaded. Run ticker_download.py first.",
                    ticker_count = count,
                });
            }
            catch (Exception ex)
            {
                return Results.Json(new
                {
                    status = "error",
                    message = ex.Message,
                    ticker_count = 0,
                }, statusCode: 500);
            }
        });

        app.MapPost("/db_reset", (Database db) =>
        {
            try
            {
                db.DropAllTables();
                return Results.Ok(new
                {
                    status = "ok",
                    message = "Database reset. Call /init_db to recreate schema.",
                });
            }
            catch (Exception ex)
            {
                return Results.Json(new
                {
                    status = "error",
                    message = ex.Message,
                }, statusCode: 500);
            }
        });
    }
}
