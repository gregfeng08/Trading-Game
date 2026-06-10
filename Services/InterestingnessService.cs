using Microsoft.Data.Sqlite;
using TradingGame.Data;
using TradingGame.Models;

namespace TradingGame.Services;

[Flags]
public enum InterestSignals
{
    None = 0,
    MarketVolatility = 1,
    TopMovers = 2,
    MarketBreadth = 4,
    PlayerEvents = 8,
    KnowledgeTriggers = 16,

    MarketOnly = MarketVolatility | TopMovers | MarketBreadth,
    All = MarketOnly | PlayerEvents | KnowledgeTriggers
}

public class InterestingnessService
{
    private readonly Database _db;
    private readonly PlayerContextService _playerContext;
    private readonly KnowledgeGraphService _knowledgeGraph;

    public InterestingnessService(Database db, PlayerContextService playerContext,
        KnowledgeGraphService knowledgeGraph)
    {
        _db = db;
        _playerContext = playerContext;
        _knowledgeGraph = knowledgeGraph;
    }

    public (double Score, List<EventSignal> Signals) Evaluate(
        string gameDate, string phase, int? entityId,
        InterestSignals include = InterestSignals.All)
    {
        using var conn = _db.Open();
        var signals = new List<EventSignal>();
        double score = 0;

        if (include.HasFlag(InterestSignals.MarketVolatility))
        {
            var (s, sig) = ScoreMarketVolatility(conn, gameDate);
            score += s;
            if (sig is not null) signals.Add(sig);
        }

        if (include.HasFlag(InterestSignals.TopMovers))
        {
            bool includeHeld = include.HasFlag(InterestSignals.PlayerEvents);
            var (s, sigs) = ScoreTopMovers(conn, gameDate, includeHeld ? entityId : null, includeHeld ? phase : null);
            score += s;
            signals.AddRange(sigs);
        }

        if (include.HasFlag(InterestSignals.MarketBreadth))
        {
            var (s, sig) = ScoreMarketBreadth(conn, gameDate);
            score += s;
            if (sig is not null) signals.Add(sig);
        }

        if (include.HasFlag(InterestSignals.PlayerEvents) && entityId.HasValue)
        {
            var ctx = _playerContext.BuildContext(entityId.Value, gameDate, phase ?? "pre_market");
            var (s, sigs) = ScorePlayerEvents(ctx);
            score += s;
            signals.AddRange(sigs);
        }

        if (include.HasFlag(InterestSignals.KnowledgeTriggers) && entityId.HasValue)
        {
            var triggered = _knowledgeGraph.EvaluateTriggers(entityId.Value, gameDate);
            if (triggered.Count > 0)
            {
                double triggerScore = Math.Min(triggered.Count * 0.1, 0.25);
                score += triggerScore;
                foreach (var node in triggered)
                    signals.Add(new EventSignal("knowledge_trigger", $"Unlocked: {node.Title}", triggerScore / triggered.Count));
            }
        }

        return (Math.Min(score, 1.0), signals);
    }

    private (double, EventSignal?) ScoreMarketVolatility(SqliteConnection conn, string gameDate)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT AVG(ABS((tp.close_price - prev.close_price) / prev.close_price * 100))
            FROM ticker_prices tp
            JOIN ticker_prices prev ON prev.ticker_id = tp.ticker_id
                AND prev.date = (SELECT MAX(date) FROM ticker_prices WHERE ticker_id = tp.ticker_id AND date < @date)
            WHERE tp.date = @date AND prev.close_price > 0;
            """;
        cmd.Parameters.AddWithValue("@date", gameDate);
        var result = cmd.ExecuteScalar();
        if (result is null or DBNull) return (0, null);

        var avgChange = Convert.ToDouble(result);
        if (avgChange < 1.5) return (0, null);

        double score = Math.Min(avgChange / 10.0, 0.3);
        return (score, new EventSignal("market_volatility",
            $"Average stock movement: {avgChange:F1}%", score));
    }

    private (double, List<EventSignal>) ScoreTopMovers(SqliteConnection conn, string gameDate,
        int? entityId, string? phase)
    {
        var signals = new List<EventSignal>();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT tp.ticker_id, tp.close_price, prev.close_price,
                   (tp.close_price - prev.close_price) / prev.close_price * 100 as pct
            FROM ticker_prices tp
            JOIN ticker_prices prev ON prev.ticker_id = tp.ticker_id
                AND prev.date = (SELECT MAX(date) FROM ticker_prices WHERE ticker_id = tp.ticker_id AND date < @date)
            WHERE tp.date = @date AND prev.close_price > 0
            ORDER BY ABS((tp.close_price - prev.close_price) / prev.close_price * 100) DESC
            LIMIT 5;
            """;
        cmd.Parameters.AddWithValue("@date", gameDate);

        var movers = new List<(string Ticker, double Close, double PrevClose, double Pct)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            movers.Add((reader.GetString(0), reader.GetDouble(1), reader.GetDouble(2), reader.GetDouble(3)));

        if (movers.Count == 0) return (0, signals);

        double topPct = movers.Max(m => Math.Abs(m.Pct));
        double score = 0;

        if (topPct >= 8)
        {
            score = 0.2;
            var top = movers.First(m => Math.Abs(m.Pct) == topPct);
            signals.Add(new EventSignal("big_mover",
                $"{top.Ticker} moved {top.Pct:+0.0;-0.0}% (${top.PrevClose:F2} → ${top.Close:F2})", 0.2));
        }
        else if (topPct >= 5)
        {
            score = 0.1;
        }

        if (entityId.HasValue && phase is not null)
        {
            var ctx = _playerContext.BuildContext(entityId.Value, gameDate, phase);
            var heldTickers = ctx.Portfolio.Holdings.Select(h => h.TickerId).ToHashSet();
            foreach (var m in movers.Where(m => heldTickers.Contains(m.Ticker) && Math.Abs(m.Pct) >= 3))
            {
                score += 0.05;
                signals.Add(new EventSignal("held_stock_moved",
                    $"Your holding {m.Ticker} moved {m.Pct:+0.0;-0.0}%", 0.05));
            }
        }

        return (Math.Min(score, 0.3), signals);
    }

    private (double, EventSignal?) ScoreMarketBreadth(SqliteConnection conn, string gameDate)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                SUM(CASE WHEN tp.close_price > prev.close_price THEN 1 ELSE 0 END) as up,
                SUM(CASE WHEN tp.close_price < prev.close_price THEN 1 ELSE 0 END) as down,
                COUNT(*) as total
            FROM ticker_prices tp
            JOIN ticker_prices prev ON prev.ticker_id = tp.ticker_id
                AND prev.date = (SELECT MAX(date) FROM ticker_prices WHERE ticker_id = tp.ticker_id AND date < @date)
            WHERE tp.date = @date AND prev.close_price > 0;
            """;
        cmd.Parameters.AddWithValue("@date", gameDate);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return (0, null);

        int up = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
        int down = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
        int total = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
        if (total == 0) return (0, null);

        double upPct = (double)up / total * 100;
        double downPct = (double)down / total * 100;

        if (upPct >= 80)
            return (0.1, new EventSignal("broad_rally", $"{upPct:F0}% of stocks advanced today", 0.1));
        if (downPct >= 80)
            return (0.1, new EventSignal("broad_selloff", $"{downPct:F0}% of stocks declined today", 0.1));

        return (0, null);
    }

    private (double, List<EventSignal>) ScorePlayerEvents(PlayerContext ctx)
    {
        var signals = new List<EventSignal>();
        double score = 0;

        int tradesToday = ctx.RecentTrades.TodaysTrades.Count;
        if (tradesToday > 0)
        {
            score += 0.05;
            signals.Add(new EventSignal("player_traded", $"You made {tradesToday} trade(s) today", 0.05));
        }

        if (ctx.Portfolio.NetWorth > 0 && ctx.Portfolio.CashRatioPct < 10)
        {
            score += 0.05;
            signals.Add(new EventSignal("low_cash", $"Cash reserves at {ctx.Portfolio.CashRatioPct:F0}% of portfolio", 0.05));
        }

        return (Math.Min(score, 0.2), signals);
    }
}
