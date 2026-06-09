using Microsoft.Data.Sqlite;
using TradingGame.Data;
using TradingGame.Models;

namespace TradingGame.Services;

public class GameStateService
{
    private readonly Database _db;
    private readonly EntityService _entities;
    private PlayerContextService? _playerContext;

    public GameStateService(Database db, EntityService entities)
    {
        _db = db;
        _entities = entities;
    }

    public void SetPlayerContextService(PlayerContextService pcs) => _playerContext = pcs;

    public GameDateResponse GetGameDate()
    {
        using var conn = _db.Open();
        var date = GetSaveValue(conn, "current_date");

        if (date is null)
            return new GameDateResponse("ok", null, null, "No active game. Call /new_game to start.");

        var phase = GetSaveValue(conn, "game_phase") ?? "pre_market";
        return new GameDateResponse("ok", date, phase, null);
    }

    public GamePhaseResponse GetGamePhase()
    {
        using var conn = _db.Open();
        var phase = GetSaveValue(conn, "game_phase");
        if (phase is null)
            return new GamePhaseResponse("ok", null, "No active game.");
        return new GamePhaseResponse("ok", phase, null);
    }

    public AdvanceDayResponse AdvanceDay(int? entityDbId = null)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        var currentDate = GetSaveValue(conn, "current_date", tx)
            ?? throw new InvalidOperationException("No active game. Call /new_game first.");

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT MIN(date) FROM ticker_prices WHERE date > @d;";
        cmd.Parameters.AddWithValue("@d", currentDate);
        var next = cmd.ExecuteScalar();

        if (next is null or DBNull)
        {
            tx.Rollback();
            return new AdvanceDayResponse("ok", null, currentDate, "pre_market", true, "No more trading days. Game complete.");
        }

        var nextDate = (string)next;
        SetSaveValue(conn, "current_date", nextDate, tx);
        SetSaveValue(conn, "game_phase", "pre_market", tx);

        // Force-liquidate any held tickers with no data on the new date
        List<ForcedLiquidationDto>? liquidations = null;
        if (entityDbId.HasValue)
            liquidations = ForceLiquidateDelistedTickers(conn, tx, entityDbId.Value, nextDate, currentDate);

        if (entityDbId.HasValue)
            SnapshotNetWorth(conn, tx, entityDbId.Value, nextDate, "pre_market");

        tx.Commit();
        _playerContext?.InvalidateCache();

        return new AdvanceDayResponse("ok", currentDate, nextDate, "pre_market", false, null)
        {
            ForcedLiquidations = liquidations?.Count > 0 ? liquidations : null
        };
    }

    private List<ForcedLiquidationDto> ForceLiquidateDelistedTickers(
        SqliteConnection conn, SqliteTransaction tx, int entityDbId, string newDate, string previousDate)
    {
        var liquidations = new List<ForcedLiquidationDto>();

        // Get all distinct tickers held by this entity
        var heldTickers = new List<(string TickerId, double TotalShares)>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT ticker_id, SUM(shares_held) FROM portfolio WHERE entity_id = @eid GROUP BY ticker_id HAVING SUM(shares_held) > 0;";
            cmd.Parameters.AddWithValue("@eid", entityDbId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                heldTickers.Add((reader.GetString(0), reader.GetDouble(1)));
        }

        foreach (var (tickerId, shares) in heldTickers)
        {
            // Check if ticker has data on the new date (or any future date)
            bool hasDataOnNewDate;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT COUNT(*) FROM ticker_prices WHERE ticker_id = @tid AND date >= @d LIMIT 1;";
                cmd.Parameters.AddWithValue("@tid", tickerId);
                cmd.Parameters.AddWithValue("@d", newDate);
                hasDataOnNewDate = Convert.ToInt32(cmd.ExecuteScalar()!) > 0;
            }

            if (hasDataOnNewDate) continue;

            // Get last known close price
            double lastClose;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT close_price FROM ticker_prices WHERE ticker_id = @tid AND date <= @prev ORDER BY date DESC LIMIT 1;";
                cmd.Parameters.AddWithValue("@tid", tickerId);
                cmd.Parameters.AddWithValue("@prev", previousDate);
                var result = cmd.ExecuteScalar();
                if (result is null or DBNull) continue;
                lastClose = Convert.ToDouble(result);
            }

            // Liquidate: credit cash, remove portfolio lots
            double proceeds = shares * lastClose;
            _entities.CreditCash(conn, tx, entityDbId, proceeds);

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM portfolio WHERE entity_id = @eid AND ticker_id = @tid;";
                cmd.Parameters.AddWithValue("@eid", entityDbId);
                cmd.Parameters.AddWithValue("@tid", tickerId);
                cmd.ExecuteNonQuery();
            }

            liquidations.Add(new ForcedLiquidationDto(tickerId, shares, lastClose, "Delisted — no further trading data"));
        }

        return liquidations;
    }

    public WeekReviewResponse AdvanceWeek(int entityDbId, int days = 5)
    {
        var dailySummaries = new List<DaySkipSummaryDto>();
        var allUnlocked = new List<UnlockedNodeDto>();
        string? startDate = null;
        string currentDate = "";

        for (int i = 0; i < days; i++)
        {
            var advanceResult = AdvanceDay(entityDbId);
            if (advanceResult.GameOver)
                break;

            currentDate = advanceResult.CurrentDate;
            startDate ??= advanceResult.PreviousDate ?? currentDate;

            using var conn = _db.Open();

            var entity = _entities.GetEntity(conn, entityDbId);
            double cash = entity?.AvailableCash ?? 0;

            double holdingsValue = 0;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT COALESCE(SUM(pf.shares_held * tp.close_price), 0)
                    FROM portfolio pf
                    JOIN ticker_prices tp ON tp.ticker_id = pf.ticker_id AND tp.date = @date
                    WHERE pf.entity_id = @eid AND pf.shares_held > 0;
                    """;
                cmd.Parameters.AddWithValue("@eid", entityDbId);
                cmd.Parameters.AddWithValue("@date", currentDate);
                holdingsValue = Convert.ToDouble(cmd.ExecuteScalar() ?? 0);
            }

            var topMovers = GetTopMovers(conn, currentDate, 3);

            dailySummaries.Add(new DaySkipSummaryDto(
                currentDate,
                Math.Round(cash + holdingsValue, 2),
                topMovers
            ));

            if (advanceResult.UnlockedNodes is { Count: > 0 })
                allUnlocked.AddRange(advanceResult.UnlockedNodes);
        }

        return new WeekReviewResponse(
            "ok",
            startDate ?? "",
            currentDate,
            dailySummaries.Count,
            dailySummaries,
            allUnlocked.Count > 0 ? allUnlocked : null
        );
    }

    private List<MoverSummaryDto> GetTopMovers(SqliteConnection conn, string date, int count)
    {
        var movers = new List<MoverSummaryDto>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT tp.ticker_id,
                   (tp.close_price - prev.close_price) / prev.close_price * 100 as pct
            FROM ticker_prices tp
            JOIN ticker_prices prev ON prev.ticker_id = tp.ticker_id
                AND prev.date = (SELECT MAX(date) FROM ticker_prices WHERE ticker_id = tp.ticker_id AND date < @date)
            WHERE tp.date = @date AND prev.close_price > 0
            ORDER BY ABS(pct) DESC
            LIMIT @n;
            """;
        cmd.Parameters.AddWithValue("@date", date);
        cmd.Parameters.AddWithValue("@n", count);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            movers.Add(new MoverSummaryDto(
                reader.GetString(0),
                Math.Round(reader.GetDouble(1), 2)
            ));
        }
        return movers;
    }

    public AdvancePhaseResponse AdvancePhase()
    {
        using var conn = _db.Open();
        var currentDate = GetSaveValue(conn, "current_date")
            ?? throw new InvalidOperationException("No active game.");

        var phase = GetSaveValue(conn, "game_phase") ?? "pre_market";

        string nextPhase = phase switch
        {
            "pre_market" => "day",
            "day" => "post_market",
            "post_market" => throw new InvalidOperationException("Already in post_market. Call /advance_day to move to next day."),
            _ => throw new InvalidOperationException($"Unknown phase: {phase}")
        };

        SetSaveValue(conn, "game_phase", nextPhase);
        return new AdvancePhaseResponse("ok", currentDate, nextPhase, null);
    }

    private const double DefaultStartingCash = 10_000.0;

    public NewGameResponse NewGame(string? startDate)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        foreach (var table in new[] { "arc_grades", "pending_orders", "newspaper", "portfolio", "trade_history", "net_worth_history", "save_state", "dynamic_npc_dialogue", "knowledge_node_progress", "player_events", "dynamic_node_content", "npc_quest_progress", "casey_daily_comments" })
        {
            using var del = conn.CreateCommand();
            del.Transaction = tx;
            del.CommandText = $"DELETE FROM \"{table}\";";
            del.ExecuteNonQuery();
        }

        // Reset all entity cash to starting amount
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE entity SET available_cash = @cash;";
            cmd.Parameters.AddWithValue("@cash", DefaultStartingCash);
            cmd.ExecuteNonQuery();
        }

        // Resolve game start date: snap to first available trading day >= requested date
        string gameStartDate;
        if (startDate is not null && startDate.Length > 0)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT MIN(date) FROM ticker_prices WHERE date >= @d;";
            cmd.Parameters.AddWithValue("@d", startDate);
            var snapped = cmd.ExecuteScalar();
            gameStartDate = (snapped is not null and not DBNull)
                ? (string)snapped
                : startDate;
        }
        else
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT MIN(date) FROM ticker_prices;";
            var min = cmd.ExecuteScalar();
            if (min is null or DBNull)
                throw new InvalidOperationException("No ticker data loaded. Load tickers first.");
            gameStartDate = (string)min;
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO save_state (key, value) VALUES ('current_date', @d);";
            cmd.Parameters.AddWithValue("@d", gameStartDate);
            cmd.ExecuteNonQuery();
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO save_state (key, value) VALUES ('game_phase', 'pre_market');";
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        _playerContext?.InvalidateCache();
        return new NewGameResponse("ok", "New game started", gameStartDate, "pre_market");
    }

    public DialogueResponse GetDialogue(string? date, string? npcType, string? tickerId, string? category)
    {
        using var conn = _db.Open();
        date ??= GetSaveValue(conn, "current_date");

        using var cmd = conn.CreateCommand();
        var clauses = new List<string>();

        if (date is not null)
        {
            cmd.Parameters.AddWithValue("@d", date);
        }
        if (npcType is not null)
        {
            clauses.Add("npc_type = @npc");
            cmd.Parameters.AddWithValue("@npc", npcType.Trim().ToLowerInvariant());
        }
        if (tickerId is not null)
        {
            clauses.Add("ticker_id = @tid");
            cmd.Parameters.AddWithValue("@tid", tickerId.ToUpperInvariant().Trim());
        }
        if (category is not null)
        {
            clauses.Add("category = @cat");
            cmd.Parameters.AddWithValue("@cat", category.Trim().ToLowerInvariant());
        }

        var where = clauses.Count > 0 ? "WHERE " + string.Join(" AND ", clauses) : "";
        var staticDateClause = date is not null ? "(date = @d OR date IS NULL)" : null;
        var dynamicDateClause = date is not null ? "date = @d" : null;

        var staticWhere = clauses.Count > 0 || staticDateClause is not null
            ? "WHERE " + string.Join(" AND ", staticDateClause is not null
                ? clauses.Prepend(staticDateClause) : clauses)
            : "";
        var dynamicWhere = clauses.Count > 0 || dynamicDateClause is not null
            ? "WHERE " + string.Join(" AND ", dynamicDateClause is not null
                ? clauses.Prepend(dynamicDateClause) : clauses)
            : "";

        cmd.CommandText = $"""
            SELECT id, date, ticker_id, npc_type, category, text, 'static' AS source,
                   priority, phase, line_order
            FROM static_npc_dialogue {staticWhere}
            UNION ALL
            SELECT id, date, ticker_id, npc_type, category, text, 'dynamic' AS source,
                   priority, phase, line_order
            FROM dynamic_npc_dialogue {dynamicWhere}
            ORDER BY date, npc_type, line_order;
            """;

        var rows = new List<DialogueRowDto>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new DialogueRowDto(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? 0 : reader.GetInt32(9)
            ));
        }

        return new DialogueResponse("ok", date, rows.Count, rows);
    }

    private void SnapshotNetWorth(SqliteConnection conn, SqliteTransaction tx, int entityDbId, string date, string phase)
    {
        var entity = _entities.GetEntity(conn, entityDbId, tx);
        if (entity is null) return;

        bool useClose = phase == "close";
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
                var price = useClose
                    ? TradingDbOps.GetClosePrice(conn, tid, date, tx)
                    : TradingDbOps.GetOpenPrice(conn, tid, date, tx);
                if (price.HasValue)
                    holdingsValue += shares * price.Value;
            }
        }

        double netWorth = entity.AvailableCash + holdingsValue;

        using var insert = conn.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = """
            INSERT OR REPLACE INTO net_worth_history (entity_id, date, phase, cash, holdings_value, net_worth)
            VALUES (@eid, @d, @phase, @cash, @hv, @nw);
            """;
        insert.Parameters.AddWithValue("@eid", entityDbId);
        insert.Parameters.AddWithValue("@d", date);
        insert.Parameters.AddWithValue("@phase", phase);
        insert.Parameters.AddWithValue("@cash", entity.AvailableCash);
        insert.Parameters.AddWithValue("@hv", holdingsValue);
        insert.Parameters.AddWithValue("@nw", netWorth);
        insert.ExecuteNonQuery();
    }

    // ── Save state CRUD ──

    public SaveStateResponse SetState(string key, string value)
    {
        using var conn = _db.Open();
        SetSaveValue(conn, key, value);
        return new SaveStateResponse("ok", key, value);
    }

    public SaveStateResponse? GetState(string key)
    {
        using var conn = _db.Open();
        var value = GetSaveValue(conn, key);
        if (value is null) return null;
        return new SaveStateResponse("ok", key, value);
    }

    // ── Helpers (also used by TradingService) ──

    public string? GetSaveValue(SqliteConnection conn, string key, SqliteTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT value FROM save_state WHERE key = @k;";
        cmd.Parameters.AddWithValue("@k", key);
        var result = cmd.ExecuteScalar();
        return result is not null and not DBNull ? (string)result : null;
    }

    public void SetSaveValue(SqliteConnection conn, string key, string value, SqliteTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO save_state (key, value) VALUES (@k, @v)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("@k", key);
        cmd.Parameters.AddWithValue("@v", value);
        cmd.ExecuteNonQuery();
    }
}
