using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using TradingGame.Data;
using TradingGame.Models;

namespace TradingGame.Services;

public class PlayerContextService
{
    private readonly Database _db;
    private readonly KnowledgeGraphService _knowledgeGraph;
    private readonly EntityService _entities;
    private readonly ArcService? _arcs;

    private readonly ConcurrentDictionary<(int EntityId, string Date), PlayerContext> _cache = new();

    public PlayerContextService(Database db, KnowledgeGraphService knowledgeGraph,
        EntityService entities, ArcService? arcs)
    {
        _db = db;
        _knowledgeGraph = knowledgeGraph;
        _entities = entities;
        _arcs = arcs;
    }

    public PlayerContext BuildContext(int entityId, string gameDate, string phase)
    {
        return _cache.GetOrAdd((entityId, gameDate), _ => BuildContextInternal(entityId, gameDate, phase));
    }

    public void InvalidateCache(int? entityId = null)
    {
        if (entityId is null)
        {
            _cache.Clear();
            return;
        }
        foreach (var key in _cache.Keys.Where(k => k.EntityId == entityId.Value))
            _cache.TryRemove(key, out _);
    }

    private PlayerContext BuildContextInternal(int entityId, string gameDate, string phase)
    {
        using var conn = _db.Open();

        var portfolio = BuildPortfolioSnapshot(conn, entityId, gameDate);
        var concentration = BuildConcentrationSnapshot(portfolio);
        var knowledge = BuildKnowledgeSnapshot(conn, entityId, gameDate);
        var trades = BuildRecentTradesSnapshot(conn, entityId, gameDate);
        var arc = BuildArcSnapshot(conn, entityId, gameDate);
        var market = BuildMarketSnapshot(conn, gameDate, phase);

        return new PlayerContext(entityId, gameDate, phase,
            portfolio, concentration, knowledge, trades, arc, market);
    }

    private PortfolioSnapshot BuildPortfolioSnapshot(SqliteConnection conn, int entityId, string gameDate)
    {
        using var cashCmd = conn.CreateCommand();
        cashCmd.CommandText = "SELECT available_cash FROM entity WHERE entity_id = @eid;";
        cashCmd.Parameters.AddWithValue("@eid", entityId);
        var cash = Convert.ToDouble(cashCmd.ExecuteScalar() ?? 0);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT pf.ticker_id,
                   SUM(pf.shares_held) as total_shares,
                   SUM(pf.shares_held * pf.price) / SUM(pf.shares_held) as avg_cost,
                   COALESCE(tp.close_price, 0) as current_price
            FROM portfolio pf
            LEFT JOIN ticker_prices tp ON tp.ticker_id = pf.ticker_id AND tp.date = @date
            WHERE pf.entity_id = @eid AND pf.shares_held > 0
            GROUP BY pf.ticker_id;
            """;
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@date", gameDate);

        var holdings = new List<HoldingSnapshot>();
        double totalHoldingsValue = 0;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var ticker = reader.GetString(0);
            var shares = reader.GetDouble(1);
            var avgCost = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);
            var currentPrice = reader.GetDouble(3);
            var marketValue = shares * currentPrice;
            var pnlPct = avgCost > 0 ? (currentPrice - avgCost) / avgCost * 100.0 : 0;

            holdings.Add(new HoldingSnapshot(ticker, shares, Math.Round(avgCost, 2),
                currentPrice, Math.Round(marketValue, 2), Math.Round(pnlPct, 2)));
            totalHoldingsValue += marketValue;
        }

        var netWorth = cash + totalHoldingsValue;
        var cashRatio = netWorth > 0 ? cash / netWorth * 100.0 : 100.0;

        return new PortfolioSnapshot(holdings, Math.Round(totalHoldingsValue, 2),
            Math.Round(cash, 2), Math.Round(netWorth, 2), Math.Round(cashRatio, 2));
    }

    private static ConcentrationSnapshot BuildConcentrationSnapshot(PortfolioSnapshot portfolio)
    {
        if (portfolio.Holdings.Count == 0)
            return new ConcentrationSnapshot(0, null, 0);

        var total = portfolio.TotalHoldingsValue;
        if (total <= 0)
            return new ConcentrationSnapshot(0, null, portfolio.Holdings.Count);

        var max = portfolio.Holdings.MaxBy(h => h.MarketValue);
        var maxPct = max is not null ? max.MarketValue / total * 100.0 : 0;

        return new ConcentrationSnapshot(Math.Round(maxPct, 2),
            max?.TickerId, portfolio.Holdings.Count);
    }

    private KnowledgeSnapshot BuildKnowledgeSnapshot(SqliteConnection conn, int entityId, string gameDate)
    {
        var completed = new List<KnowledgeNodeSummary>();
        var unlockedNotCompleted = new List<KnowledgeNodeSummary>();
        var recentlyUnlocked = new List<KnowledgeNodeSummary>();

        var nodeConfigs = _knowledgeGraph.Config.Nodes.ToDictionary(n => n.Id);
        int totalNodes = nodeConfigs.Count;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT node_id, status, unlocked_at FROM knowledge_node_progress WHERE entity_id = @eid;";
        cmd.Parameters.AddWithValue("@eid", entityId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var nodeId = reader.GetString(0);
            var status = reader.GetString(1);
            var unlockedAt = reader.IsDBNull(2) ? null : reader.GetString(2);

            if (!nodeConfigs.TryGetValue(nodeId, out var config)) continue;
            var summary = new KnowledgeNodeSummary(nodeId, config.Title);

            if (status == "completed")
                completed.Add(summary);
            else if (status == "unlocked")
                unlockedNotCompleted.Add(summary);

            if (unlockedAt == gameDate)
                recentlyUnlocked.Add(summary);
        }

        return new KnowledgeSnapshot(completed, unlockedNotCompleted,
            totalNodes, completed.Count, recentlyUnlocked);
    }

    private RecentTradesSnapshot BuildRecentTradesSnapshot(SqliteConnection conn, int entityId, string gameDate)
    {
        var todaysTrades = new List<TradeSnapshot>();
        var lastNTrades = new List<TradeSnapshot>();

        using var todayCmd = conn.CreateCommand();
        todayCmd.CommandText = """
            SELECT ticker_id, shares, price_paid, trade_date
            FROM trade_history WHERE entity_id = @eid AND trade_date = @date
            ORDER BY history_id DESC;
            """;
        todayCmd.Parameters.AddWithValue("@eid", entityId);
        todayCmd.Parameters.AddWithValue("@date", gameDate);

        using (var reader = todayCmd.ExecuteReader())
        {
            while (reader.Read())
            {
                todaysTrades.Add(new TradeSnapshot(
                    reader.GetString(0), reader.GetDouble(1),
                    reader.GetDouble(2), reader.GetString(3)));
            }
        }

        using var lastCmd = conn.CreateCommand();
        lastCmd.CommandText = """
            SELECT ticker_id, shares, price_paid, trade_date
            FROM trade_history WHERE entity_id = @eid
            ORDER BY history_id DESC LIMIT 10;
            """;
        lastCmd.Parameters.AddWithValue("@eid", entityId);

        using (var reader = lastCmd.ExecuteReader())
        {
            while (reader.Read())
            {
                lastNTrades.Add(new TradeSnapshot(
                    reader.GetString(0), reader.GetDouble(1),
                    reader.GetDouble(2), reader.GetString(3)));
            }
        }

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM trade_history WHERE entity_id = @eid;";
        countCmd.Parameters.AddWithValue("@eid", entityId);
        var totalCount = Convert.ToInt32(countCmd.ExecuteScalar());

        return new RecentTradesSnapshot(todaysTrades, lastNTrades, totalCount);
    }

    private ArcSnapshot BuildArcSnapshot(SqliteConnection conn, int entityId, string gameDate)
    {
        if (_arcs is null)
            return new ArcSnapshot(null, null, null, null, null);

        var allArcs = _arcs.GetAllArcs();
        var currentArc = allArcs.FirstOrDefault(a =>
            string.Compare(gameDate, a.StartDate, StringComparison.Ordinal) >= 0 &&
            string.Compare(gameDate, a.EndDate, StringComparison.Ordinal) <= 0);

        if (currentArc is null)
            return new ArcSnapshot(null, null, null, null, null);

        using var daysCmd = conn.CreateCommand();
        daysCmd.CommandText = "SELECT COUNT(DISTINCT date) FROM ticker_prices WHERE date > @c AND date <= @e;";
        daysCmd.Parameters.AddWithValue("@c", gameDate);
        daysCmd.Parameters.AddWithValue("@e", currentArc.EndDate);
        var daysRemaining = Convert.ToInt32(daysCmd.ExecuteScalar());

        var startKey = $"arc_start_value_{currentArc.Id}_{entityId}";
        using var startCmd = conn.CreateCommand();
        startCmd.CommandText = "SELECT value FROM save_state WHERE key = @k;";
        startCmd.Parameters.AddWithValue("@k", startKey);
        var startValStr = startCmd.ExecuteScalar() as string;

        double? returnPct = null;
        string? grade = null;

        if (startValStr is not null && double.TryParse(startValStr, out var startVal) && startVal > 0)
        {
            var entity = _entities.GetEntity(conn, entityId);
            double cash = entity?.AvailableCash ?? 0;

            using var hvCmd = conn.CreateCommand();
            hvCmd.CommandText = """
                SELECT COALESCE(SUM(pf.shares_held * tp.close_price), 0)
                FROM portfolio pf
                JOIN ticker_prices tp ON tp.ticker_id = pf.ticker_id AND tp.date = @date
                WHERE pf.entity_id = @eid AND pf.shares_held > 0;
                """;
            hvCmd.Parameters.AddWithValue("@eid", entityId);
            hvCmd.Parameters.AddWithValue("@date", gameDate);
            var holdingsVal = Convert.ToDouble(hvCmd.ExecuteScalar() ?? 0);

            var currentVal = cash + holdingsVal;
            returnPct = Math.Round((currentVal - startVal) / startVal * 100.0, 2);

            if (returnPct >= currentArc.GradeThresholds.GetValueOrDefault("S", double.MaxValue)) grade = "S";
            else if (returnPct >= currentArc.GradeThresholds.GetValueOrDefault("A", double.MaxValue)) grade = "A";
            else if (returnPct >= currentArc.GradeThresholds.GetValueOrDefault("B", double.MaxValue)) grade = "B";
            else if (returnPct >= currentArc.GradeThresholds.GetValueOrDefault("C", double.MaxValue)) grade = "C";
            else grade = "D";
        }

        return new ArcSnapshot(currentArc.Name, daysRemaining, returnPct, grade, currentArc.NewspaperTone);
    }

    private MarketSnapshot BuildMarketSnapshot(SqliteConnection conn, string gameDate, string phase)
    {
        var gainers = new List<MarketMoverSnapshot>();
        var losers = new List<MarketMoverSnapshot>();
        int advancing = 0, declining = 0, total = 0;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT t.ticker_id, t.close_price,
                   (t.close_price - p.close_price) / p.close_price * 100 as pct_change
            FROM ticker_prices t
            JOIN ticker_prices p ON t.ticker_id = p.ticker_id
                AND p.date = (SELECT MAX(date) FROM ticker_prices WHERE ticker_id = t.ticker_id AND date < @date)
            WHERE t.date = @date AND p.close_price > 0
            ORDER BY t.ticker_id;
            """;
        cmd.Parameters.AddWithValue("@date", gameDate);

        var allMovers = new List<MarketMoverSnapshot>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader.IsDBNull(2)) continue;
            var ticker = reader.GetString(0);
            var closePrice = reader.GetDouble(1);
            var pctChange = reader.GetDouble(2);

            allMovers.Add(new MarketMoverSnapshot(ticker, Math.Round(closePrice, 2), Math.Round(pctChange, 2)));
            total++;
            if (pctChange > 0) advancing++;
            else if (pctChange < 0) declining++;
        }

        var sorted = allMovers.OrderByDescending(m => m.ChangePct).ToList();
        gainers.AddRange(sorted.Take(5));
        losers.AddRange(sorted.TakeLast(5).Reverse());

        return new MarketSnapshot(gameDate, phase, gainers, losers, advancing, declining, total);
    }
}
