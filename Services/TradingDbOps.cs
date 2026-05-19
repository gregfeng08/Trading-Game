using Microsoft.Data.Sqlite;

namespace TradingGame.Services;

internal static class TradingDbOps
{
    public static (double Open, double High, double Low, double Close)? GetOHLC(
        SqliteConnection conn, string tickerId, string dateIso, SqliteTransaction? tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT open_price, high_price, low_price, close_price FROM ticker_prices WHERE ticker_id = @tid AND date = @d;";
        cmd.Parameters.AddWithValue("@tid", tickerId);
        cmd.Parameters.AddWithValue("@d", dateIso);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return (reader.GetDouble(0), reader.GetDouble(1), reader.GetDouble(2), reader.GetDouble(3));
    }

    public static double? GetPreviousClosePrice(
        SqliteConnection conn, string tickerId, string currentDate, SqliteTransaction? tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT close_price FROM ticker_prices WHERE ticker_id = @tid AND date < @d ORDER BY date DESC LIMIT 1;";
        cmd.Parameters.AddWithValue("@tid", tickerId);
        cmd.Parameters.AddWithValue("@d", currentDate);
        var result = cmd.ExecuteScalar();
        return result is not null ? Convert.ToDouble(result) : null;
    }

    public static double? GetClosePrice(
        SqliteConnection conn, string tickerId, string dateIso, SqliteTransaction? tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT close_price FROM ticker_prices WHERE ticker_id = @tid AND date = @d;";
        cmd.Parameters.AddWithValue("@tid", tickerId);
        cmd.Parameters.AddWithValue("@d", dateIso);
        var result = cmd.ExecuteScalar();
        return result is not null ? Convert.ToDouble(result) : null;
    }

    public static double? GetOpenPrice(
        SqliteConnection conn, string tickerId, string dateIso, SqliteTransaction? tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT open_price FROM ticker_prices WHERE ticker_id = @tid AND date = @d;";
        cmd.Parameters.AddWithValue("@tid", tickerId);
        cmd.Parameters.AddWithValue("@d", dateIso);
        var result = cmd.ExecuteScalar();
        return result is not null ? Convert.ToDouble(result) : null;
    }

    public static double GetTotalShares(
        SqliteConnection conn, int entityId, string tickerId, SqliteTransaction? tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT COALESCE(SUM(shares_held), 0) FROM portfolio WHERE entity_id = @eid AND ticker_id = @tid;";
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@tid", tickerId);
        return Convert.ToDouble(cmd.ExecuteScalar()!);
    }

    public static void InsertPortfolioLot(SqliteConnection conn, SqliteTransaction tx,
        int entityId, string tickerId, double shares, string date, double price)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO portfolio (entity_id, ticker_id, shares_held, purchase_date, price) VALUES (@eid, @tid, @sh, @d, @p);";
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@tid", tickerId);
        cmd.Parameters.AddWithValue("@sh", shares);
        cmd.Parameters.AddWithValue("@d", date);
        cmd.Parameters.AddWithValue("@p", price);
        cmd.ExecuteNonQuery();
    }

    public static void InsertTradeHistory(SqliteConnection conn, SqliteTransaction tx,
        int entityId, string tickerId, double price, double shares, string date,
        string? tradePhase = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO trade_history (entity_id, ticker_id, price_paid, shares, trade_date, trade_phase) VALUES (@eid, @tid, @pp, @sh, @d, @tp);";
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@tid", tickerId);
        cmd.Parameters.AddWithValue("@pp", price);
        cmd.Parameters.AddWithValue("@sh", shares);
        cmd.Parameters.AddWithValue("@d", date);
        cmd.Parameters.AddWithValue("@tp", (object?)tradePhase ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public static void SellFifo(SqliteConnection conn, SqliteTransaction tx,
        int entityId, string tickerId, double sharesToSell)
    {
        var lots = new List<(int Id, double Shares)>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                SELECT portfolio_id, shares_held
                FROM portfolio
                WHERE entity_id = @eid AND ticker_id = @tid
                ORDER BY purchase_date ASC, portfolio_id ASC;
                """;
            cmd.Parameters.AddWithValue("@eid", entityId);
            cmd.Parameters.AddWithValue("@tid", tickerId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                lots.Add((reader.GetInt32(0), reader.GetDouble(1)));
        }

        double remaining = sharesToSell;
        foreach (var (lotId, lotShares) in lots)
        {
            if (remaining <= 0) break;

            double take = Math.Min(lotShares, remaining);
            double newShares = lotShares - take;

            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;

            if (newShares <= 1e-12)
            {
                cmd.CommandText = "DELETE FROM portfolio WHERE portfolio_id = @pid;";
                cmd.Parameters.AddWithValue("@pid", lotId);
            }
            else
            {
                cmd.CommandText = "UPDATE portfolio SET shares_held = @sh WHERE portfolio_id = @pid;";
                cmd.Parameters.AddWithValue("@sh", newShares);
                cmd.Parameters.AddWithValue("@pid", lotId);
            }
            cmd.ExecuteNonQuery();
            remaining -= take;
        }

        if (remaining > 1e-9)
            throw new InvalidOperationException("FIFO sell failed: not enough shares in lots.");
    }
}
