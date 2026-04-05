using Microsoft.Data.Sqlite;
using TradingGame.Data;

namespace TradingGame.Endpoints;

public static class MarketEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/tickers", (Database db) =>
        {
            using var conn = db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT ticker_id, company_name, description FROM loaded_ticker_list ORDER BY ticker_id;";

            var tickers = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                tickers.Add(new
                {
                    ticker_id = reader.GetString(0),
                    company_name = reader.IsDBNull(1) ? null : reader.GetString(1),
                    description = reader.IsDBNull(2) ? null : reader.GetString(2),
                });
            }

            return Results.Ok(new { status = "ok", tickers });
        });

        app.MapGet("/prices", (string tickerId, string? startDate, string? endDate, Database db) =>
        {
            using var conn = db.Open();
            using var cmd = conn.CreateCommand();

            var tid = tickerId.ToUpperInvariant().Trim();

            if (startDate is not null && endDate is not null)
            {
                cmd.CommandText = """
                    SELECT ticker_id, date, open_price, high_price, low_price, close_price
                    FROM ticker_prices
                    WHERE ticker_id = @tid AND date BETWEEN @start AND @end
                    ORDER BY date;
                    """;
                cmd.Parameters.AddWithValue("@tid", tid);
                cmd.Parameters.AddWithValue("@start", startDate);
                cmd.Parameters.AddWithValue("@end", endDate);
            }
            else
            {
                cmd.CommandText = """
                    SELECT ticker_id, date, open_price, high_price, low_price, close_price
                    FROM ticker_prices
                    WHERE ticker_id = @tid
                    ORDER BY date;
                    """;
                cmd.Parameters.AddWithValue("@tid", tid);
            }

            var rows = ReadPriceRows(cmd);
            return Results.Ok(new { status = "ok", ticker_id = tid, rows });
        });

        // Unity expects: { data: [{ ticker, date, open, high, low, close, volume, technicalData: { sma20, sma50, sma200 } }] }
        app.MapGet("/get_daily_data", (string? ticker, int? limit, Database db) =>
        {
            int lim = limit ?? 500;

            using var conn = db.Open();
            using var cmd = conn.CreateCommand();

            if (ticker is not null)
            {
                cmd.CommandText = """
                    SELECT ticker_id, date, open_price, high_price, low_price, close_price
                    FROM ticker_prices
                    WHERE ticker_id = @tid
                    ORDER BY date DESC
                    LIMIT @lim;
                    """;
                cmd.Parameters.AddWithValue("@tid", ticker.ToUpperInvariant().Trim());
                cmd.Parameters.AddWithValue("@lim", lim);
            }
            else
            {
                cmd.CommandText = """
                    SELECT ticker_id, date, open_price, high_price, low_price, close_price
                    FROM ticker_prices
                    ORDER BY date DESC
                    LIMIT @lim;
                    """;
                cmd.Parameters.AddWithValue("@lim", lim);
            }

            var data = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                data.Add(new
                {
                    ticker = reader.GetString(0),
                    date = reader.GetString(1),
                    open = reader.IsDBNull(2) ? 0f : (float)reader.GetDouble(2),
                    high = reader.IsDBNull(3) ? 0f : (float)reader.GetDouble(3),
                    low = reader.IsDBNull(4) ? 0f : (float)reader.GetDouble(4),
                    close = reader.IsDBNull(5) ? 0f : (float)reader.GetDouble(5),
                    volume = 0f,
                    technicalData = new
                    {
                        sma20 = 0f,
                        sma50 = 0f,
                        sma200 = 0f,
                    },
                });
            }

            return Results.Ok(new { data });
        });
    }

    private static List<object> ReadPriceRows(SqliteCommand cmd)
    {
        var rows = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new
            {
                ticker_id = reader.GetString(0),
                date = reader.GetString(1),
                open_price = reader.IsDBNull(2) ? 0.0 : reader.GetDouble(2),
                high_price = reader.IsDBNull(3) ? 0.0 : reader.GetDouble(3),
                low_price = reader.IsDBNull(4) ? 0.0 : reader.GetDouble(4),
                close_price = reader.IsDBNull(5) ? 0.0 : reader.GetDouble(5),
            });
        }
        return rows;
    }
}
