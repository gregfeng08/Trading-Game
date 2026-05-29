using Microsoft.Data.Sqlite;
using TradingGame.Data;
using TradingGame.Models;

namespace TradingGame.Services;

public class TradingService
{
    private readonly Database _db;
    private readonly EntityService _entities;
    private readonly GameStateService _gameState;

    public TradingService(Database db, EntityService entities, GameStateService gameState)
    {
        _db = db;
        _entities = entities;
        _gameState = gameState;
    }

    // ── External trade (called by Unity via /post_trade) ──

    public IResult PostTrade(PostTradeRequest req)
    {
        var tickerId = req.Ticker.ToUpperInvariant().Trim();
        var side = req.Side.Trim().ToLowerInvariant();
        var orderType = (req.OrderType ?? "market").Trim().ToLowerInvariant();

        if (side is not "buy" and not "sell")
            return Results.Json(new ErrorResponse("error", "side must be 'buy' or 'sell'"), statusCode: 400);

        if (req.Quantity <= 0)
            return Results.Json(new ErrorResponse("error", "quantity must be positive"), statusCode: 400);

        if (orderType == "limit" && req.Price <= 0)
            return Results.Json(new ErrorResponse("error", "limit orders require a positive price"), statusCode: 400);

        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            var gameDate = _gameState.GetSaveValue(conn, "current_date", tx);
            if (gameDate is null)
            {
                tx.Rollback();
                return Results.Json(new ErrorResponse("error", "No active game. Call /new_game first."), statusCode: 400);
            }

            var gamePhase = _gameState.GetSaveValue(conn, "game_phase", tx) ?? "pre_market";

            var ohlc = TradingDbOps.GetOHLC(conn, tickerId, gameDate, tx);
            if (ohlc is null)
            {
                tx.Rollback();
                return Results.Json(new ErrorResponse("error", $"No price data for {tickerId} on {gameDate}"), statusCode: 400);
            }

            double price;
            if (orderType == "limit")
            {
                double limitPrice = req.Price;
                if (side == "buy")
                {
                    if (ohlc.Value.Low > limitPrice)
                    {
                        tx.Rollback();
                        return Results.Ok(new PostTradeResponse("not_filled", $"Buy limit {limitPrice:F2} not reached. Day low was {ohlc.Value.Low:F2}", 0, orderType));
                    }
                    price = limitPrice;
                }
                else
                {
                    if (ohlc.Value.High < limitPrice)
                    {
                        tx.Rollback();
                        return Results.Ok(new PostTradeResponse("not_filled", $"Sell limit {limitPrice:F2} not reached. Day high was {ohlc.Value.High:F2}", 0, orderType));
                    }
                    price = limitPrice;
                }
            }
            else
            {
                if (gamePhase == "post_market")
                    price = ohlc.Value.Close;
                else
                    price = ohlc.Value.Open;
            }

            var entityDbId = _entities.ResolveExternalId(conn, req.EntityId, tx);
            if (entityDbId is null)
            {
                tx.Rollback();
                return Results.Json(new ErrorResponse("error", $"Entity '{req.EntityId}' not found"), statusCode: 404);
            }

            var entity = _entities.GetEntity(conn, entityDbId.Value, tx);
            if (entity is null)
            {
                tx.Rollback();
                return Results.Json(new ErrorResponse("error", "Entity not found"), statusCode: 404);
            }

            double currentCash = entity.AvailableCash;
            double quantity = req.Quantity;

            if (side == "buy")
            {
                double cost = price * quantity;
                if (currentCash + 1e-9 < cost)
                {
                    tx.Rollback();
                    return Results.Json(new ErrorResponse("error", "Insufficient cash"), statusCode: 400);
                }

                _entities.UpdateCash(conn, tx, entityDbId.Value, currentCash - cost);
                TradingDbOps.InsertPortfolioLot(conn, tx, entityDbId.Value, tickerId, quantity, gameDate, price);
                TradingDbOps.InsertTradeHistory(conn, tx, entityDbId.Value, tickerId, price, quantity, gameDate, gamePhase);

                tx.Commit();
                return Results.Ok(new PostTradeResponse("ok", null, price, orderType));
            }
            else
            {
                double held = TradingDbOps.GetTotalShares(conn, entityDbId.Value, tickerId, tx);
                if (held + 1e-9 < quantity)
                {
                    tx.Rollback();
                    return Results.Json(new ErrorResponse("error", "Insufficient shares"), statusCode: 400);
                }

                _entities.UpdateCash(conn, tx, entityDbId.Value, currentCash + price * quantity);
                TradingDbOps.SellFifo(conn, tx, entityDbId.Value, tickerId, quantity);
                TradingDbOps.InsertTradeHistory(conn, tx, entityDbId.Value, tickerId, price, -quantity, gameDate, gamePhase);

                tx.Commit();
                return Results.Ok(new PostTradeResponse("ok", null, price, orderType));
            }
        }
        catch (Exception)
        {
            try { tx.Rollback(); } catch { }
            return Results.Json(new ErrorResponse("error", "An internal error occurred while processing the trade."), statusCode: 500);
        }
    }

    // ── Internal trade (uses internal entity_id + date) ──

    public IResult Trade(TradeRequest req)
    {
        var tickerId = req.TickerId.ToUpperInvariant().Trim();
        var shares = req.Shares;
        var dateIso = req.Date;

        if (!DateOnly.TryParseExact(dateIso, "yyyy-MM-dd", out _))
            return Results.Json(new ErrorResponse("error", "date must be YYYY-MM-DD"), statusCode: 400);

        if (Math.Abs(shares) < 1e-12)
            return Results.Json(new ErrorResponse("error", "shares cannot be 0"), statusCode: 400);

        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            var entity = _entities.GetEntity(conn, req.EntityId, tx);
            if (entity is null)
            {
                tx.Rollback();
                return Results.Json(new ErrorResponse("error", "Entity not found"), statusCode: 404);
            }

            var price = TradingDbOps.GetClosePrice(conn, tickerId, dateIso, tx);
            if (price is null)
            {
                tx.Rollback();
                return Results.Json(new ErrorResponse("error", $"No price for {tickerId} on {dateIso}"), statusCode: 400);
            }

            double currentCash = entity.AvailableCash;

            if (shares > 0)
                return ExecuteBuy(conn, tx, req.EntityId, tickerId, shares, dateIso, price.Value, currentCash);
            else
                return ExecuteSell(conn, tx, req.EntityId, tickerId, -shares, dateIso, price.Value, currentCash);
        }
        catch (Exception)
        {
            try { tx.Rollback(); } catch { }
            return Results.Json(new ErrorResponse("error", "An internal error occurred while processing the trade."), statusCode: 500);
        }
    }

    // ── Portfolio ──

    public PortfolioResponse? GetPortfolio(int entityId)
    {
        using var conn = _db.Open();

        var entity = _entities.GetEntity(conn, entityId);
        if (entity is null) return null;

        var lots = new List<PortfolioLotDto>();
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
                lots.Add(new PortfolioLotDto(
                    reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2),
                    reader.GetDouble(3), reader.GetString(4), reader.GetDouble(5)
                ));
            }
        }

        var rawTotals = new List<(string TickerId, double Shares)>();
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
                rawTotals.Add((reader.GetString(0), reader.GetDouble(1)));
        }

        var gameDate = _gameState.GetSaveValue(conn, "current_date");
        var phase = _gameState.GetSaveValue(conn, "game_phase") ?? "pre_market";
        bool useClose = phase == "post_market";
        string priceBasis = useClose ? "close" : "open";

        double holdingsValue = 0;
        var totals = new List<PortfolioTotalDto>();
        foreach (var (tickerId, shares) in rawTotals)
        {
            double curPrice = 0;
            if (gameDate is not null)
            {
                double? price = useClose
                    ? TradingDbOps.GetClosePrice(conn, tickerId, gameDate, null)
                    : TradingDbOps.GetOpenPrice(conn, tickerId, gameDate, null);
                curPrice = price ?? 0;
            }
            double mktVal = shares * curPrice;
            holdingsValue += mktVal;
            totals.Add(new PortfolioTotalDto(tickerId, shares,
                Math.Round(curPrice, 4), Math.Round(mktVal, 2)));
        }

        double netWorth = entity.AvailableCash + holdingsValue;

        return new PortfolioResponse("ok", entity, lots, totals,
            Math.Round(holdingsValue, 2), Math.Round(netWorth, 2), priceBasis);
    }

    // ── Trade history ──

    public TradeHistoryResponse GetTradeHistory(int? entityId, string? tickerId)
    {
        using var conn = _db.Open();
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

        var rows = new List<TradeHistoryRowDto>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new TradeHistoryRowDto(
                reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2),
                reader.GetDouble(3), reader.GetDouble(4), reader.GetString(5)
            ));
        }

        return new TradeHistoryResponse("ok", rows);
    }

    // ── Private helpers ──

    private IResult ExecuteBuy(SqliteConnection conn, SqliteTransaction tx,
        int entityId, string tickerId, double shares, string dateIso, double price, double currentCash)
    {
        double cost = price * shares;
        if (currentCash + 1e-9 < cost)
        {
            tx.Rollback();
            return Results.Json(new ErrorResponse("error", "Insufficient cash"), statusCode: 400);
        }

        double newCash = currentCash - cost;
        _entities.UpdateCash(conn, tx, entityId, newCash);
        TradingDbOps.InsertPortfolioLot(conn, tx, entityId, tickerId, shares, dateIso, price);
        TradingDbOps.InsertTradeHistory(conn, tx, entityId, tickerId, price, shares, dateIso);

        tx.Commit();
        return Results.Ok(new InternalTradeResponse("ok", "BUY", entityId, tickerId, shares, price, currentCash, newCash));
    }

    private IResult ExecuteSell(SqliteConnection conn, SqliteTransaction tx,
        int entityId, string tickerId, double sellQty, string dateIso, double price, double currentCash)
    {
        double held = TradingDbOps.GetTotalShares(conn, entityId, tickerId, tx);
        if (held + 1e-9 < sellQty)
        {
            tx.Rollback();
            return Results.Json(new ErrorResponse("error", "Insufficient shares"), statusCode: 400);
        }

        double newCash = currentCash + price * sellQty;
        _entities.UpdateCash(conn, tx, entityId, newCash);
        TradingDbOps.SellFifo(conn, tx, entityId, tickerId, sellQty);
        TradingDbOps.InsertTradeHistory(conn, tx, entityId, tickerId, price, -sellQty, dateIso);

        tx.Commit();
        return Results.Ok(new InternalTradeResponse("ok", "SELL", entityId, tickerId, -sellQty, price, currentCash, newCash));
    }
}
