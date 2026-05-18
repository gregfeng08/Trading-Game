using TradingGame.Data;
using TradingGame.Models;
using TradingGame.Services;

namespace TradingGame.Endpoints;

public static class SystemEndpoints
{
    private static readonly DateTimeOffset StartTime = DateTimeOffset.UtcNow;

    public static void Map(WebApplication app)
    {
        app.MapGet("/", () => Results.Ok(new { message = "Trading Game API" }));

        app.MapGet("/ping", () =>
            Results.Ok(new PingResponse("ok", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0)));

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
            catch { dbOk = false; }

            return Results.Ok(new StatusResponse(
                "ok",
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
                (DateTimeOffset.UtcNow - StartTime).TotalSeconds,
                dbOk,
                "dotnet-dev"
            ));
        });

        app.MapPost("/init_db", (Database db) =>
        {
            try
            {
                db.InitSchema();
                return Results.Ok(new InitDbResponse("ok", "Database initialized", true));
            }
            catch (Exception ex)
            {
                return Results.Json(new InitDbResponse("error", ex.Message, false), statusCode: 500);
            }
        });

        app.MapPost("/load_tickers", async (MarketDataService market, LoadTickersRequest? req) =>
        {
            try
            {
                var existing = market.GetTickerCount();
                if (existing > 0)
                    return Results.Ok(new LoadTickerDataResponse("ok", $"{existing} tickers already loaded", existing));

                var startDate = req?.StartDate ?? "2005-01-01";
                var endDate = req?.EndDate ?? "2010-12-31";
                var topN = req?.TopN ?? 50;

                var result = await market.LoadTickersAsync(startDate, endDate, topN);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Json(new LoadTickerDataResponse("error", ex.Message, 0), statusCode: 500);
            }
        });

        app.MapPost("/db_reset", (Database db) =>
        {
            try
            {
                db.DropAllTables();
                return Results.Ok(new DbResetResponse("ok", "Database reset. Call /init_db to recreate schema."));
            }
            catch (Exception ex)
            {
                return Results.Json(new DbResetResponse("error", ex.Message), statusCode: 500);
            }
        });
    }
}
