using Microsoft.Data.Sqlite;
using TradingGame.Data;
using TradingGame.Models;

namespace TradingGame.Endpoints;

public static class TradeEndpoints
{
    public static void Map(WebApplication app)
    {
        // Unity calls this endpoint.
        // Accepts: { entity_id (string/external), ticker, side, quantity, price, order_type }
        // Server validates price against the current game date's historical close price.
        // Returns: { status }
        app.MapPost("/post_trade", (PostTradeRequest req, Database db) =>
        {
            var tickerId = req.Ticker.ToUpperInvariant().Trim();
            var side = req.Side.Trim().ToLowerInvariant();

            if (side != "buy" && side != "sell")
                return Results.Json(new { status = "error", message = "side must be 'buy' or 'sell'" }, statusCode: 400);

            if (req.Quantity <= 0)
                return Results.Json(new { status = "error", message = "quantity must be positive" }, statusCode: 400);

            using var conn = db.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                // Get current game date
                var gameDate = GameEndpoints.GetSaveValue(conn, "current_date", tx);
                if (gameDate is null)
                {
                    tx.Rollback();
                    return Results.Json(new { status = "error", message = "No active game. Call /new_game first." }, statusCode: 400);
                }

                // Look up the actual historical close price for this ticker on the game date
                var historicalPrice = GetClosePrice(conn, tickerId, gameDate, tx);
                if (historicalPrice is null)
                {
                    tx.Rollback();
                    return Results.Json(new { status = "error", message = $"No price data for {tickerId} on {gameDate}" }, statusCode: 400);
                }
                double price = historicalPrice.Value;

                // Resolve external entity_id to internal DB id
                var entityDbId = EntityEndpoints.ResolveExternalId(conn, req.EntityId, tx);
                if (entityDbId is null)
                {
                    tx.Rollback();
                    return Results.Json(new { status = "error", message = $"Entity '{req.EntityId}' not found" }, statusCode: 404);
                }

                var entity = EntityEndpoints.GetEntityDict(conn, entityDbId.Value, tx);
                if (entity is null)
                {
                    tx.Rollback();
                    return Results.Json(new { status = "error", message = "Entity not found" }, statusCode: 404);
                }

                double currentCash = (double)entity["available_cash"];
                double quantity = req.Quantity;

                if (side == "buy")
                {
                    double cost = price * quantity;
                    if (currentCash + 1e-9 < cost)
                    {
                        tx.Rollback();
                        return Results.Json(new { status = "error", message = "Insufficient cash" }, statusCode: 400);
                    }

                    double newCash = currentCash - cost;
                    UpdateCash(conn, tx, entityDbId.Value, newCash);
                    InsertPortfolioLot(conn, tx, entityDbId.Value, tickerId, quantity, gameDate, price);
                    InsertTradeHistory(conn, tx, entityDbId.Value, tickerId, price, quantity, gameDate);

                    tx.Commit();
                    return Results.Ok(new { status = "ok" });
                }
                else // sell
                {
                    double held = GetTotalShares(conn, entityDbId.Value, tickerId, tx);
                    if (held + 1e-9 < quantity)
                    {
                        tx.Rollback();
                        return Results.Json(new { status = "error", message = "Insufficient shares" }, statusCode: 400);
                    }

                    double proceeds = price * quantity;
                    double newCash = currentCash + proceeds;
                    UpdateCash(conn, tx, entityDbId.Value, newCash);
                    SellFifo(conn, tx, entityDbId.Value, tickerId, quantity);
                    InsertTradeHistory(conn, tx, entityDbId.Value, tickerId, price, -quantity, gameDate);

                    tx.Commit();
                    return Results.Ok(new { status = "ok" });
                }
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { }
                return Results.Json(new { status = "error", message = ex.Message }, statusCode: 500);
            }
        });

        // Internal trade endpoint (uses internal entity_id int + date for price lookup)
        app.MapPost("/trade", (TradeRequest req, Database db) =>
        {
            var tickerId = req.TickerId.ToUpperInvariant().Trim();
            var shares = req.Shares;
            var dateIso = req.Date;

            if (!DateOnly.TryParseExact(dateIso, "yyyy-MM-dd", out _))
                return Results.Json(new { status = "error", message = "date must be YYYY-MM-DD" }, statusCode: 400);

            if (Math.Abs(shares) < 1e-12)
                return Results.Json(new { status = "error", message = "shares cannot be 0" }, statusCode: 400);

            using var conn = db.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                var entity = EntityEndpoints.GetEntityDict(conn, req.EntityId, tx);
                if (entity is null)
                {
                    tx.Rollback();
                    return Results.Json(new { status = "error", message = "Entity not found" }, statusCode: 404);
                }

                var price = GetClosePrice(conn, tickerId, dateIso, tx);
                if (price is null)
                {
                    tx.Rollback();
                    return Results.Json(new { status = "error", message = $"No price for {tickerId} on {dateIso}" }, statusCode: 400);
                }

                double currentCash = (double)entity["available_cash"];

                if (shares > 0)
                    return ExecuteBuy(conn, tx, req.EntityId, tickerId, shares, dateIso, price.Value, currentCash);
                else
                    return ExecuteSell(conn, tx, req.EntityId, tickerId, -shares, dateIso, price.Value, currentCash);
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { }
                return Results.Json(new { status = "error", message = ex.Message }, statusCode: 500);
            }
        });

        app.MapGet("/portfolio", (int entityId, Database db) =>
        {
            using var conn = db.Open();

            var entity = EntityEndpoints.GetEntityDict(conn, entityId);
            if (entity is null)
                return Results.Json(new { status = "error", message = "Entity not found" }, statusCode: 404);

            var lots = new List<object>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT portfolio_id, entity_id, ticker_id, shares_held, purchase_date, price
                    FROM portfolio WHERE entity_id = @eid
                    ORDER BY ticker_id, purchase_date;
                    """;
                cmd.Parameters.AddWithValue("@eid", entityId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    lots.Add(new
                    {
                        portfolio_id = reader.GetInt32(0),
                        entity_id = reader.GetInt32(1),
                        ticker_id = reader.GetString(2),
                        shares_held = reader.GetDouble(3),
                        purchase_date = reader.GetString(4),
                        price = reader.GetDouble(5),
                    });
                }
            }

            var totals = new List<object>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT ticker_id, COALESCE(SUM(shares_held), 0)
                    FROM portfolio WHERE entity_id = @eid
                    GROUP BY ticker_id ORDER BY ticker_id;
                    """;
                cmd.Parameters.AddWithValue("@eid", entityId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    totals.Add(new
                    {
                        ticker_id = reader.GetString(0),
                        shares_held = reader.GetDouble(1),
                    });
                }
            }

            return Results.Ok(new { status = "ok", entity, lots, totals });
        });

        app.MapGet("/trade_history", (int? entityId, string? tickerId, Database db) =>
        {
            using var conn = db.Open();
            using var cmd = conn.CreateCommand();

            var clauses = new List<string>();
            if (entityId.HasValue)
            {
                clauses.Add("entity_id = @eid");
                cmd.Parameters.AddWithValue("@eid", entityId.Value);
            }
            if (tickerId is not null)
            {
                clauses.Add("ticker_id = @tid");
                cmd.Parameters.AddWithValue("@tid", tickerId);
            }

            var where = clauses.Count > 0 ? "WHERE " + string.Join(" AND ", clauses) : "";
            cmd.CommandText = $"""
                SELECT history_id, entity_id, ticker_id, price_paid, shares, trade_date
                FROM trade_history {where}
                ORDER BY trade_date, history_id;
                """;

            var rows = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new
                {
                    history_id = reader.GetInt32(0),
                    entity_id = reader.GetInt32(1),
                    ticker_id = reader.GetString(2),
                    price_paid = reader.GetDouble(3),
                    shares = reader.GetDouble(4),
                    trade_date = reader.GetString(5),
                });
            }

            return Results.Ok(new { status = "ok", rows });
        });
    }

    // ---- shared helpers ----

    private static double? GetClosePrice(SqliteConnection conn, string tickerId, string dateIso, SqliteTransaction tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT close_price FROM ticker_prices WHERE ticker_id = @tid AND date = @d;";
        cmd.Parameters.AddWithValue("@tid", tickerId);
        cmd.Parameters.AddWithValue("@d", dateIso);

        var result = cmd.ExecuteScalar();
        return result is not null ? Convert.ToDouble(result) : null;
    }

    private static double GetTotalShares(SqliteConnection conn, int entityId, string tickerId, SqliteTransaction tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT COALESCE(SUM(shares_held), 0) FROM portfolio WHERE entity_id = @eid AND ticker_id = @tid;";
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@tid", tickerId);

        return Convert.ToDouble(cmd.ExecuteScalar()!);
    }

    private static void UpdateCash(SqliteConnection conn, SqliteTransaction tx, int entityId, double newCash)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE entity SET available_cash = @cash WHERE entity_id = @eid;";
        cmd.Parameters.AddWithValue("@cash", newCash);
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.ExecuteNonQuery();
    }

    private static void InsertPortfolioLot(SqliteConnection conn, SqliteTransaction tx,
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

    private static void InsertTradeHistory(SqliteConnection conn, SqliteTransaction tx,
        int entityId, string tickerId, double price, double shares, string date)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO trade_history (entity_id, ticker_id, price_paid, shares, trade_date) VALUES (@eid, @tid, @pp, @sh, @d);";
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@tid", tickerId);
        cmd.Parameters.AddWithValue("@pp", price);
        cmd.Parameters.AddWithValue("@sh", shares);
        cmd.Parameters.AddWithValue("@d", date);
        cmd.ExecuteNonQuery();
    }

    private static void SellFifo(SqliteConnection conn, SqliteTransaction tx,
        int entityId, string tickerId, double sharesToSell)
    {
        var lots = new List<(int Id, double Shares, string Date, double Price)>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                SELECT portfolio_id, shares_held, purchase_date, price
                FROM portfolio
                WHERE entity_id = @eid AND ticker_id = @tid
                ORDER BY purchase_date ASC, portfolio_id ASC;
                """;
            cmd.Parameters.AddWithValue("@eid", entityId);
            cmd.Parameters.AddWithValue("@tid", tickerId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                lots.Add((reader.GetInt32(0), reader.GetDouble(1), reader.GetString(2), reader.GetDouble(3)));
        }

        double remaining = sharesToSell;
        foreach (var (lotId, lotShares, _, _) in lots)
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

    private static IResult ExecuteBuy(SqliteConnection conn, SqliteTransaction tx,
        int entityId, string tickerId, double shares, string dateIso, double price, double currentCash)
    {
        double cost = price * shares;
        if (currentCash + 1e-9 < cost)
        {
            tx.Rollback();
            return Results.Json(new
            {
                status = "error",
                message = "Insufficient cash",
                available_cash = currentCash,
                required_cash = cost,
                price,
            }, statusCode: 400);
        }

        double newCash = currentCash - cost;
        UpdateCash(conn, tx, entityId, newCash);
        InsertPortfolioLot(conn, tx, entityId, tickerId, shares, dateIso, price);
        InsertTradeHistory(conn, tx, entityId, tickerId, price, shares, dateIso);

        tx.Commit();
        return Results.Ok(new
        {
            status = "ok",
            side = "BUY",
            entity_id = entityId,
            ticker_id = tickerId,
            shares,
            price,
            cash_before = currentCash,
            cash_after = newCash,
        });
    }

    private static IResult ExecuteSell(SqliteConnection conn, SqliteTransaction tx,
        int entityId, string tickerId, double sellQty, string dateIso, double price, double currentCash)
    {
        double held = GetTotalShares(conn, entityId, tickerId, tx);
        if (held + 1e-9 < sellQty)
        {
            tx.Rollback();
            return Results.Json(new
            {
                status = "error",
                message = "Insufficient shares",
                shares_held = held,
                shares_requested_to_sell = sellQty,
            }, statusCode: 400);
        }

        double proceeds = price * sellQty;
        double newCash = currentCash + proceeds;
        UpdateCash(conn, tx, entityId, newCash);
        SellFifo(conn, tx, entityId, tickerId, sellQty);
        InsertTradeHistory(conn, tx, entityId, tickerId, price, -sellQty, dateIso);

        tx.Commit();
        return Results.Ok(new
        {
            status = "ok",
            side = "SELL",
            entity_id = entityId,
            ticker_id = tickerId,
            shares = -sellQty,
            price,
            cash_before = currentCash,
            cash_after = newCash,
        });
    }
}
