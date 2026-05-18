using Microsoft.Data.Sqlite;
using TradingGame.Data;
using TradingGame.Models;

namespace TradingGame.Services;

public class OrderService
{
    private readonly Database _db;
    private readonly EntityService _entities;
    private readonly GameStateService _gameState;
    private readonly KnowledgeGraphService _knowledgeGraph;

    public OrderService(Database db, EntityService entities, GameStateService gameState, KnowledgeGraphService knowledgeGraph)
    {
        _db = db;
        _entities = entities;
        _gameState = gameState;
        _knowledgeGraph = knowledgeGraph;
    }

    public IResult QueueOrder(QueueOrderRequest req)
    {
        var ticker = req.Ticker.ToUpperInvariant().Trim();
        var side = req.Side.Trim().ToLowerInvariant();

        if (side is not "buy" and not "sell")
            return Results.Json(new ErrorResponse("error", "side must be 'buy' or 'sell'"), statusCode: 400);
        if (req.Quantity <= 0)
            return Results.Json(new ErrorResponse("error", "quantity must be positive"), statusCode: 400);

        var orderType = (req.OrderType ?? "market").Trim().ToLowerInvariant();
        if (orderType == "limit" && (req.LimitPrice is null || req.LimitPrice <= 0))
            return Results.Json(new ErrorResponse("error", "limit orders require a positive limit_price"), statusCode: 400);

        using var conn = _db.Open();

        var entityDbId = _entities.ResolveExternalId(conn, req.EntityId);
        if (entityDbId is null)
            return Results.Json(new ErrorResponse("error", $"Entity '{req.EntityId}' not found"), statusCode: 404);

        var phase = _gameState.GetSaveValue(conn, "game_phase");
        if (phase != "pre_market")
            return Results.Json(new ErrorResponse("error", "Orders can only be queued during pre_market"), statusCode: 400);

        var gameDate = _gameState.GetSaveValue(conn, "current_date");

        // Estimate fill price for validation (use today's open, fallback to prev close)
        double estimatedPrice;
        if (orderType == "limit")
        {
            estimatedPrice = req.LimitPrice!.Value;
        }
        else
        {
            var ohlc = GetOHLC(conn, ticker, gameDate!, null);
            if (ohlc is not null)
                estimatedPrice = ohlc.Value.Open;
            else
            {
                var prevClose = GetPreviousClosePrice(conn, ticker, gameDate!, null);
                if (prevClose is null)
                    return Results.Json(new ErrorResponse("error", $"No price data for {ticker}"), statusCode: 400);
                estimatedPrice = prevClose.Value;
            }
        }

        if (side == "buy")
        {
            var entity = _entities.GetEntity(conn, entityDbId.Value);
            if (entity is null)
                return Results.Json(new ErrorResponse("error", "Entity not found"), statusCode: 404);

            double pendingBuyCost = GetPendingBuyCost(conn, entityDbId.Value, gameDate!);
            double cost = estimatedPrice * req.Quantity;
            if (entity.AvailableCash - pendingBuyCost < cost - 1e-9)
                return Results.Json(new ErrorResponse("error",
                    $"Insufficient cash. Need ${cost:F2}, available ${entity.AvailableCash - pendingBuyCost:F2}"), statusCode: 400);
        }
        else
        {
            double held = GetTotalShares(conn, entityDbId.Value, ticker, null);
            double pendingSellQty = GetPendingSellQuantity(conn, entityDbId.Value, ticker);
            if (held - pendingSellQty < req.Quantity - 1e-9)
                return Results.Json(new ErrorResponse("error",
                    $"Insufficient shares. Hold {held - pendingSellQty:F0}, trying to sell {req.Quantity}"), statusCode: 400);
        }

        // Check for existing matching order to batch with
        int? existingOrderId = FindMatchingOrder(conn, entityDbId.Value, ticker, side, orderType, req.LimitPrice);

        int orderId;
        if (existingOrderId.HasValue)
        {
            using var upd = conn.CreateCommand();
            upd.CommandText = "UPDATE pending_orders SET quantity = quantity + @qty WHERE order_id = @oid;";
            upd.Parameters.AddWithValue("@qty", req.Quantity);
            upd.Parameters.AddWithValue("@oid", existingOrderId.Value);
            upd.ExecuteNonQuery();
            orderId = existingOrderId.Value;
        }
        else
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO pending_orders (entity_id, ticker_id, side, quantity, order_type, limit_price, queued_at)
                VALUES (@eid, @tid, @side, @qty, @ot, @lp, @qa);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("@eid", entityDbId.Value);
            cmd.Parameters.AddWithValue("@tid", ticker);
            cmd.Parameters.AddWithValue("@side", side);
            cmd.Parameters.AddWithValue("@qty", req.Quantity);
            cmd.Parameters.AddWithValue("@ot", orderType);
            cmd.Parameters.AddWithValue("@lp", (object?)req.LimitPrice ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@qa", DateTime.UtcNow.ToString("o"));
            orderId = Convert.ToInt32(cmd.ExecuteScalar()!);
        }

        return Results.Ok(new QueueOrderResponse("ok", orderId, null));
    }

    private double GetPendingBuyCost(SqliteConnection conn, int entityDbId, string gameDate)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT po.ticker_id, po.quantity, po.order_type, po.limit_price
            FROM pending_orders po WHERE po.entity_id = @eid AND po.side = 'buy';
            """;
        cmd.Parameters.AddWithValue("@eid", entityDbId);

        double total = 0;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var tid = reader.GetString(0);
            var qty = reader.GetInt32(1);
            var ot = reader.GetString(2);
            double price;
            if (ot == "limit" && !reader.IsDBNull(3))
                price = reader.GetDouble(3);
            else
            {
                var ohlc = GetOHLC(conn, tid, gameDate, null);
                price = ohlc?.Open ?? 0;
            }
            total += price * qty;
        }
        return total;
    }

    private double GetPendingSellQuantity(SqliteConnection conn, int entityDbId, string ticker)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(quantity), 0) FROM pending_orders WHERE entity_id = @eid AND ticker_id = @tid AND side = 'sell';";
        cmd.Parameters.AddWithValue("@eid", entityDbId);
        cmd.Parameters.AddWithValue("@tid", ticker);
        return Convert.ToDouble(cmd.ExecuteScalar()!);
    }

    private int? FindMatchingOrder(SqliteConnection conn, int entityDbId, string ticker, string side, string orderType, double? limitPrice)
    {
        using var cmd = conn.CreateCommand();
        if (orderType == "limit" && limitPrice.HasValue)
        {
            cmd.CommandText = """
                SELECT order_id FROM pending_orders
                WHERE entity_id = @eid AND ticker_id = @tid AND side = @side
                  AND order_type = @ot AND limit_price = @lp
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("@lp", limitPrice.Value);
        }
        else
        {
            cmd.CommandText = """
                SELECT order_id FROM pending_orders
                WHERE entity_id = @eid AND ticker_id = @tid AND side = @side
                  AND order_type = @ot AND limit_price IS NULL
                LIMIT 1;
                """;
        }
        cmd.Parameters.AddWithValue("@eid", entityDbId);
        cmd.Parameters.AddWithValue("@tid", ticker);
        cmd.Parameters.AddWithValue("@side", side);
        cmd.Parameters.AddWithValue("@ot", orderType);

        var result = cmd.ExecuteScalar();
        return result is not null ? Convert.ToInt32(result) : null;
    }

    public PendingOrdersResponse GetPendingOrders(int entityDbId)
    {
        using var conn = _db.Open();
        return GetPendingOrders(conn, entityDbId);
    }

    public IResult ClearPendingOrders(string externalId)
    {
        using var conn = _db.Open();
        var entityDbId = _entities.ResolveExternalId(conn, externalId);
        if (entityDbId is null)
            return Results.Json(new ErrorResponse("error", "Entity not found"), statusCode: 404);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM pending_orders WHERE entity_id = @eid;";
        cmd.Parameters.AddWithValue("@eid", entityDbId.Value);
        cmd.ExecuteNonQuery();

        return Results.Ok(new { status = "ok", message = "All pending orders cleared" });
    }

    public IResult RemoveOrder(int orderId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM pending_orders WHERE order_id = @oid;";
        cmd.Parameters.AddWithValue("@oid", orderId);
        var rows = cmd.ExecuteNonQuery();

        if (rows == 0)
            return Results.Json(new ErrorResponse("error", "Order not found"), statusCode: 404);
        return Results.Ok(new { status = "ok", message = "Order removed" });
    }

    public IResult OpenMarkets(OpenMarketsRequest req)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            var gameDate = _gameState.GetSaveValue(conn, "current_date", tx)
                ?? throw new InvalidOperationException("No active game.");
            var phase = _gameState.GetSaveValue(conn, "game_phase", tx) ?? "pre_market";

            if (phase != "pre_market")
            {
                tx.Rollback();
                return Results.Json(new ErrorResponse("error", $"Cannot open markets: current phase is '{phase}'"), statusCode: 400);
            }

            var entityDbId = _entities.ResolveExternalId(conn, req.EntityId, tx);
            if (entityDbId is null)
            {
                tx.Rollback();
                return Results.Json(new ErrorResponse("error", $"Entity '{req.EntityId}' not found"), statusCode: 404);
            }

            _gameState.SetSaveValue(conn, "game_phase", "day", tx);

            var orders = LoadPendingOrders(conn, entityDbId.Value, tx);
            var results = new List<TradeResultDto>();

            foreach (var order in orders)
            {
                var result = ExecuteOrder(conn, tx, entityDbId.Value, order, gameDate, "day");
                results.Add(result);
            }

            DeletePendingOrders(conn, entityDbId.Value, tx);

            tx.Commit();

            var unlocked = _knowledgeGraph.EvaluateTriggers(entityDbId.Value, gameDate);

            return Results.Ok(new OpenMarketsResponse(
                "ok", gameDate, "day", results,
                unlocked.Count > 0 ? unlocked : null, null));
        }
        catch (Exception ex)
        {
            try { tx.Rollback(); } catch { }
            return Results.Json(new ErrorResponse("error", ex.Message), statusCode: 500);
        }
    }

    public IResult CloseMarkets(CloseMarketsRequest req)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            var gameDate = _gameState.GetSaveValue(conn, "current_date", tx)
                ?? throw new InvalidOperationException("No active game.");
            var phase = _gameState.GetSaveValue(conn, "game_phase", tx) ?? "pre_market";

            if (phase != "day")
            {
                tx.Rollback();
                return Results.Json(new ErrorResponse("error", $"Cannot close markets: current phase is '{phase}'"), statusCode: 400);
            }

            var entityDbId = _entities.ResolveExternalId(conn, req.EntityId, tx);
            if (entityDbId is null)
            {
                tx.Rollback();
                return Results.Json(new ErrorResponse("error", $"Entity '{req.EntityId}' not found"), statusCode: 404);
            }

            _gameState.SetSaveValue(conn, "game_phase", "post_market", tx);

            var todayTrades = GetTodayTrades(conn, entityDbId.Value, gameDate, tx);
            var results = new List<TradeResultDto>();
            double dayPnl = 0;

            foreach (var trade in todayTrades)
            {
                var closePrice = GetClosePrice(conn, trade.TickerId, gameDate, tx);
                double? pnl = null;

                if (closePrice.HasValue)
                {
                    pnl = trade.Shares > 0
                        ? (closePrice.Value - trade.Price) * Math.Abs(trade.Shares)
                        : (trade.Price - closePrice.Value) * Math.Abs(trade.Shares);
                    dayPnl += pnl.Value;
                }

                results.Add(new TradeResultDto(
                    trade.TickerId,
                    trade.Shares > 0 ? "buy" : "sell",
                    (int)Math.Abs(trade.Shares),
                    trade.Price,
                    closePrice,
                    pnl,
                    "ok",
                    null
                ));
            }

            SnapshotNetWorth(conn, tx, entityDbId.Value, gameDate);

            tx.Commit();

            var unlocked = _knowledgeGraph.EvaluateTriggers(entityDbId.Value, gameDate);

            return Results.Ok(new CloseMarketsResponse(
                "ok", gameDate, "post_market", results, Math.Round(dayPnl, 2),
                unlocked.Count > 0 ? unlocked : null, null));
        }
        catch (Exception ex)
        {
            try { tx.Rollback(); } catch { }
            return Results.Json(new ErrorResponse("error", ex.Message), statusCode: 500);
        }
    }

    // ── Private helpers ──

    private TradeResultDto ExecuteOrder(SqliteConnection conn, SqliteTransaction tx,
        int entityDbId, PendingOrderRecord order, string gameDate, string gamePhase)
    {
        var ohlc = GetOHLC(conn, order.TickerId, gameDate, tx);
        if (ohlc is null)
            return new TradeResultDto(order.TickerId, order.Side, order.Quantity, 0, null, null, "error", $"No price data for {order.TickerId} on {gameDate}");

        double price;
        if (order.OrderType == "limit")
        {
            double limitPrice = order.LimitPrice!.Value;
            if (order.Side == "buy" && ohlc.Value.Low > limitPrice)
                return new TradeResultDto(order.TickerId, order.Side, order.Quantity, 0, null, null, "not_filled", $"Buy limit {limitPrice:F2} not reached. Day low was {ohlc.Value.Low:F2}");
            if (order.Side == "sell" && ohlc.Value.High < limitPrice)
                return new TradeResultDto(order.TickerId, order.Side, order.Quantity, 0, null, null, "not_filled", $"Sell limit {limitPrice:F2} not reached. Day high was {ohlc.Value.High:F2}");
            price = limitPrice;
        }
        else
        {
            var prevClose = GetPreviousClosePrice(conn, order.TickerId, gameDate, tx);
            if (prevClose is null)
                return new TradeResultDto(order.TickerId, order.Side, order.Quantity, 0, null, null, "error", $"No previous close price for {order.TickerId}");
            price = prevClose.Value;
        }

        var entity = _entities.GetEntity(conn, entityDbId, tx);
        if (entity is null)
            return new TradeResultDto(order.TickerId, order.Side, order.Quantity, 0, null, null, "error", "Entity not found");

        double currentCash = entity.AvailableCash;

        if (order.Side == "buy")
        {
            double cost = price * order.Quantity;
            if (currentCash + 1e-9 < cost)
                return new TradeResultDto(order.TickerId, order.Side, order.Quantity, 0, null, null, "error", "Insufficient cash");

            _entities.UpdateCash(conn, tx, entityDbId, currentCash - cost);
            InsertPortfolioLot(conn, tx, entityDbId, order.TickerId, order.Quantity, gameDate, price);
            InsertTradeHistory(conn, tx, entityDbId, order.TickerId, price, order.Quantity, gameDate);
        }
        else
        {
            double held = GetTotalShares(conn, entityDbId, order.TickerId, tx);
            if (held + 1e-9 < order.Quantity)
                return new TradeResultDto(order.TickerId, order.Side, order.Quantity, 0, null, null, "error", "Insufficient shares");

            _entities.UpdateCash(conn, tx, entityDbId, currentCash + price * order.Quantity);
            SellFifo(conn, tx, entityDbId, order.TickerId, order.Quantity);
            InsertTradeHistory(conn, tx, entityDbId, order.TickerId, price, -order.Quantity, gameDate);
        }

        return new TradeResultDto(order.TickerId, order.Side, order.Quantity, price, null, null, "ok", null);
    }

    private PendingOrdersResponse GetPendingOrders(SqliteConnection conn, int entityDbId, SqliteTransaction? tx = null)
    {
        var orders = new List<PendingOrderDto>();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT order_id, ticker_id, side, quantity, order_type, limit_price FROM pending_orders WHERE entity_id = @eid ORDER BY order_id;";
        cmd.Parameters.AddWithValue("@eid", entityDbId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            orders.Add(new PendingOrderDto(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetDouble(5)
            ));
        }
        return new PendingOrdersResponse("ok", orders);
    }

    private List<PendingOrderRecord> LoadPendingOrders(SqliteConnection conn, int entityDbId, SqliteTransaction tx)
    {
        var orders = new List<PendingOrderRecord>();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT order_id, ticker_id, side, quantity, order_type, limit_price FROM pending_orders WHERE entity_id = @eid ORDER BY order_id;";
        cmd.Parameters.AddWithValue("@eid", entityDbId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            orders.Add(new PendingOrderRecord(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetDouble(5)
            ));
        }
        return orders;
    }

    private void DeletePendingOrders(SqliteConnection conn, int entityDbId, SqliteTransaction tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM pending_orders WHERE entity_id = @eid;";
        cmd.Parameters.AddWithValue("@eid", entityDbId);
        cmd.ExecuteNonQuery();
    }

    private List<TradeRecord> GetTodayTrades(SqliteConnection conn, int entityDbId, string date, SqliteTransaction tx)
    {
        var trades = new List<TradeRecord>();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT ticker_id, price_paid, shares FROM trade_history WHERE entity_id = @eid AND trade_date = @d ORDER BY history_id;";
        cmd.Parameters.AddWithValue("@eid", entityDbId);
        cmd.Parameters.AddWithValue("@d", date);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            trades.Add(new TradeRecord(reader.GetString(0), reader.GetDouble(1), reader.GetDouble(2)));
        return trades;
    }

    private (double Open, double High, double Low, double Close)? GetOHLC(
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

    private double? GetPreviousClosePrice(SqliteConnection conn, string tickerId, string currentDate, SqliteTransaction? tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT close_price FROM ticker_prices WHERE ticker_id = @tid AND date < @d ORDER BY date DESC LIMIT 1;";
        cmd.Parameters.AddWithValue("@tid", tickerId);
        cmd.Parameters.AddWithValue("@d", currentDate);
        var result = cmd.ExecuteScalar();
        return result is not null ? Convert.ToDouble(result) : null;
    }

    private double? GetClosePrice(SqliteConnection conn, string tickerId, string dateIso, SqliteTransaction? tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT close_price FROM ticker_prices WHERE ticker_id = @tid AND date = @d;";
        cmd.Parameters.AddWithValue("@tid", tickerId);
        cmd.Parameters.AddWithValue("@d", dateIso);
        var result = cmd.ExecuteScalar();
        return result is not null ? Convert.ToDouble(result) : null;
    }

    private double GetTotalShares(SqliteConnection conn, int entityId, string tickerId, SqliteTransaction? tx)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT COALESCE(SUM(shares_held), 0) FROM portfolio WHERE entity_id = @eid AND ticker_id = @tid;";
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@tid", tickerId);
        return Convert.ToDouble(cmd.ExecuteScalar()!);
    }

    private void InsertPortfolioLot(SqliteConnection conn, SqliteTransaction tx,
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

    private void InsertTradeHistory(SqliteConnection conn, SqliteTransaction tx,
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

    private void SellFifo(SqliteConnection conn, SqliteTransaction tx,
        int entityId, string tickerId, double sharesToSell)
    {
        var lots = new List<(int Id, double Shares)>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                SELECT portfolio_id, shares_held FROM portfolio
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
    }

    private void SnapshotNetWorth(SqliteConnection conn, SqliteTransaction tx, int entityDbId, string date)
    {
        var entity = _entities.GetEntity(conn, entityDbId, tx);
        if (entity is null) return;

        double holdingsValue = 0;
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT ticker_id, SUM(shares_held) FROM portfolio WHERE entity_id = @eid GROUP BY ticker_id;";
            cmd.Parameters.AddWithValue("@eid", entityDbId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var tid = reader.GetString(0);
                var shares = reader.GetDouble(1);
                var close = GetClosePrice(conn, tid, date, tx);
                if (close.HasValue)
                    holdingsValue += shares * close.Value;
            }
        }

        double netWorth = entity.AvailableCash + holdingsValue;

        using var insert = conn.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = """
            INSERT OR REPLACE INTO net_worth_history (entity_id, date, cash, holdings_value, net_worth)
            VALUES (@eid, @d, @cash, @hv, @nw);
            """;
        insert.Parameters.AddWithValue("@eid", entityDbId);
        insert.Parameters.AddWithValue("@d", date);
        insert.Parameters.AddWithValue("@cash", entity.AvailableCash);
        insert.Parameters.AddWithValue("@hv", holdingsValue);
        insert.Parameters.AddWithValue("@nw", netWorth);
        insert.ExecuteNonQuery();
    }

    public PortfolioHistoryResponse GetPortfolioHistory(int entityDbId)
    {
        using var conn = _db.Open();
        var rows = new List<NetWorthPointDto>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT date, cash, holdings_value, net_worth FROM net_worth_history WHERE entity_id = @eid ORDER BY date;";
        cmd.Parameters.AddWithValue("@eid", entityDbId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new NetWorthPointDto(
                reader.GetString(0),
                reader.GetDouble(1),
                reader.GetDouble(2),
                reader.GetDouble(3)
            ));
        }
        return new PortfolioHistoryResponse("ok", rows);
    }

    private record PendingOrderRecord(int OrderId, string TickerId, string Side, int Quantity, string OrderType, double? LimitPrice);
    private record TradeRecord(string TickerId, double Price, double Shares);
}
