using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TradingGame.Data;
using TradingGame.Models;

namespace TradingGame.Services;

public class NpcDialogueService
{
    private readonly Database _db;
    private readonly GameStateService _gameState;
    private readonly KnowledgeGraphService _knowledgeGraph;
    private readonly PlayerContextService _playerContext;
    private readonly ArcService? _arcService;
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _model;

    private static readonly string[] NpcTypes = ["analyst", "broker", "trader", "anchor"];

    private static readonly Dictionary<string, NpcPersonality> Personalities = new()
    {
        ["analyst"] = new(
            "The Analyst",
            """
            You are a Wall Street equity analyst. Measured, data-driven, you reference specific
            numbers and percentages. You explain what the data means, not just what happened.
            Speak directly to the player as a colleague.
            """),
        ["broker"] = new(
            "The Broker",
            """
            You are a veteran stockbroker. You talk about sentiment, what "the street" is thinking,
            and the mood of the market. You use Wall Street jargon naturally. You're persuasive
            and opinionated but not pushy.
            """),
        ["trader"] = new(
            "The Trader",
            """
            You are a floor trader. Short, punchy sentences. You trust your gut and talk about
            price action. Colorful language, street-level perspective. You've seen it all before.
            """),
        ["anchor"] = new(
            "The Anchor",
            """
            You are a financial news anchor. Neutral, formal, you summarize events clearly and
            concisely. You present facts without strong opinions. Professional broadcast tone.
            """)
    };

    public NpcDialogueService(Database db, GameStateService gameState,
        KnowledgeGraphService knowledgeGraph, PlayerContextService playerContext,
        ArcService? arcService = null)
    {
        _db = db;
        _gameState = gameState;
        _knowledgeGraph = knowledgeGraph;
        _playerContext = playerContext;
        _arcService = arcService;
        _httpClient = new HttpClient();
        _apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        _model = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL") ?? "claude-sonnet-4-20250514";
    }

    // ── Main entry point: generate dialogue for a phase transition ──

    public async Task<DialogueGenerationResponse> GenerateForPhase(
        string gameDate, string phase, int? entityId)
    {
        using var conn = _db.Open();

        if (HasCachedDialogue(conn, gameDate, phase, entityId))
            return new DialogueGenerationResponse("ok", gameDate, phase, -1, 0, 0);

        var (score, signals) = EvaluateInterestingness(conn, gameDate, phase, entityId);

        int dynamicCount = 0;
        int staticCount = 0;

        if (score >= 0.5 && !string.IsNullOrEmpty(_apiKey))
        {
            foreach (var npcType in NpcTypes)
            {
                var lines = await GenerateForNpc(npcType, gameDate, phase, signals, entityId, conn);
                if (lines.Count > 0)
                {
                    CacheDynamicLines(conn, gameDate, phase, npcType, lines, signals, entityId);
                    dynamicCount += lines.Count;
                }
            }
            staticCount = SelectAndCacheStaticLines(conn, gameDate, phase, DeriveMarketMood(signals), 2);
        }
        else if (score >= 0.2 && !string.IsNullOrEmpty(_apiKey))
        {
            foreach (var npcType in new[] { "analyst", "anchor" })
            {
                var lines = await GenerateForNpc(npcType, gameDate, phase, signals, entityId, conn);
                if (lines.Count > 0)
                {
                    CacheDynamicLines(conn, gameDate, phase, npcType, lines, signals, entityId);
                    dynamicCount += lines.Count;
                }
            }
            staticCount = SelectAndCacheStaticLines(conn, gameDate, phase, DeriveMarketMood(signals), 4);
        }
        else
        {
            staticCount = SelectAndCacheStaticLines(conn, gameDate, phase, DeriveMarketMood(signals), 6);
        }

        return new DialogueGenerationResponse("ok", gameDate, phase, Math.Round(score, 3),
            dynamicCount, staticCount);
    }

    // ── Interestingness scoring ──

    private (double Score, List<EventSignal> Signals) EvaluateInterestingness(
        SqliteConnection conn, string gameDate, string phase, int? entityId)
    {
        var signals = new List<EventSignal>();
        double score = 0;

        var (volScore, volSignal) = ScoreMarketVolatility(conn, gameDate);
        score += volScore;
        if (volSignal is not null) signals.Add(volSignal);

        var (moverScore, moverSignals) = ScoreTopMovers(conn, gameDate, entityId, phase);
        score += moverScore;
        signals.AddRange(moverSignals);

        var (breadthScore, breadthSignal) = ScoreMarketBreadth(conn, gameDate);
        score += breadthScore;
        if (breadthSignal is not null) signals.Add(breadthSignal);

        if (entityId.HasValue)
        {
            var ctx = _playerContext.BuildContext(entityId.Value, gameDate, phase);

            var (portfolioScore, portfolioSignals) = ScorePlayerEvents(ctx);
            score += portfolioScore;
            signals.AddRange(portfolioSignals);

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

    private (double, List<EventSignal>) ScoreTopMovers(SqliteConnection conn, string gameDate, int? entityId, string? phase = null)
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

    // ── Fact sheet assembly ──

    private string BuildFactSheet(SqliteConnection conn, string npcType, string gameDate,
        string phase, List<EventSignal> signals, int? entityId)
    {
        PlayerContext? ctx = null;
        if (entityId.HasValue)
            ctx = _playerContext.BuildContext(entityId.Value, gameDate, phase);

        var sb = new StringBuilder();
        sb.AppendLine($"GAME DATE: {gameDate}");
        sb.AppendLine($"MARKET PHASE: {phase}");
        sb.AppendLine();

        var arcTone = ctx?.Arc.ArcTone ?? GetArcTone(gameDate);
        if (arcTone is not null)
            sb.AppendLine($"MARKET ERA MOOD: {arcTone}");

        sb.AppendLine("── TODAY'S EVENTS ──");
        foreach (var signal in signals)
            sb.AppendLine($"• {signal.Description}");
        sb.AppendLine();

        if (npcType is "analyst" or "anchor")
        {
            sb.AppendLine("── TOP MOVERS ──");
            AppendTopMovers(conn, gameDate, sb, 5);
            sb.AppendLine();

            sb.AppendLine("── MARKET BREADTH ──");
            AppendMarketBreadth(conn, gameDate, sb);
            sb.AppendLine();
        }

        if (npcType is "broker" or "trader")
        {
            sb.AppendLine("── BIGGEST MOVERS ──");
            AppendTopMovers(conn, gameDate, sb, 3);
            sb.AppendLine();
        }

        if (ctx is not null && npcType is "analyst" or "broker")
        {
            sb.AppendLine("── PLAYER PORTFOLIO ──");
            sb.AppendLine($"  Cash: ${ctx.Portfolio.Cash:F2}");
            foreach (var h in ctx.Portfolio.Holdings)
                sb.AppendLine($"  {h.TickerId}: {h.SharesHeld:F0} shares @ avg ${h.AvgCostBasis:F2}, now ${h.CurrentPrice:F2} ({h.UnrealizedPnlPct:+0.0;-0.0}%)");
            if (ctx.Concentration.MaxTicker is not null)
                sb.AppendLine($"  Concentration: {ctx.Concentration.MaxSingleTickerPct:F0}% in {ctx.Concentration.MaxTicker}");
            sb.AppendLine();
        }

        if (ctx is not null && npcType == "trader")
        {
            sb.AppendLine("── PLAYER'S RECENT ACTIVITY ──");
            if (ctx.RecentTrades.TodaysTrades.Count > 0)
            {
                foreach (var t in ctx.RecentTrades.TodaysTrades)
                {
                    var side = t.Shares > 0 ? "Bought" : "Sold";
                    sb.AppendLine($"  {side} {Math.Abs(t.Shares):F0} {t.TickerId} @ ${t.Price:F2}");
                }
            }
            else
                sb.AppendLine("  No trades today.");
            sb.AppendLine();
        }

        if (ctx is not null)
        {
            sb.AppendLine("── PLAYER KNOWLEDGE ──");
            sb.AppendLine($"  Learning progress: {ctx.Knowledge.CompletedCount}/{ctx.Knowledge.TotalNodes} concepts completed");
            if (ctx.Knowledge.RecentlyUnlocked.Count > 0)
            {
                sb.AppendLine("  Recently unlocked:");
                foreach (var node in ctx.Knowledge.RecentlyUnlocked)
                    sb.AppendLine($"    • {node.Title}");
            }
            if (ctx.Knowledge.CompletedNodes.Count > 0)
            {
                var recent = ctx.Knowledge.CompletedNodes.TakeLast(3);
                sb.AppendLine("  Recently learned:");
                foreach (var node in recent)
                    sb.AppendLine($"    • {node.Title}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private void AppendTopMovers(SqliteConnection conn, string gameDate, StringBuilder sb, int count)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT tp.ticker_id,
                   (tp.close_price - prev.close_price) / prev.close_price * 100 as pct,
                   tp.close_price
            FROM ticker_prices tp
            JOIN ticker_prices prev ON prev.ticker_id = tp.ticker_id
                AND prev.date = (SELECT MAX(date) FROM ticker_prices WHERE ticker_id = tp.ticker_id AND date < @date)
            WHERE tp.date = @date AND prev.close_price > 0
            ORDER BY ABS((tp.close_price - prev.close_price) / prev.close_price * 100) DESC
            LIMIT @n;
            """;
        cmd.Parameters.AddWithValue("@date", gameDate);
        cmd.Parameters.AddWithValue("@n", count);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var ticker = reader.GetString(0);
            var pct = reader.GetDouble(1);
            var close = reader.GetDouble(2);
            sb.AppendLine($"  {ticker}: {pct:+0.00;-0.00}% (close ${close:F2})");
        }
    }

    private void AppendMarketBreadth(SqliteConnection conn, string gameDate, StringBuilder sb)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                SUM(CASE WHEN tp.close_price > prev.close_price THEN 1 ELSE 0 END),
                SUM(CASE WHEN tp.close_price < prev.close_price THEN 1 ELSE 0 END),
                COUNT(*)
            FROM ticker_prices tp
            JOIN ticker_prices prev ON prev.ticker_id = tp.ticker_id
                AND prev.date = (SELECT MAX(date) FROM ticker_prices WHERE ticker_id = tp.ticker_id AND date < @date)
            WHERE tp.date = @date AND prev.close_price > 0;
            """;
        cmd.Parameters.AddWithValue("@date", gameDate);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            int up = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
            int down = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            int total = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
            sb.AppendLine($"  Advancing: {up}/{total}  Declining: {down}/{total}");
        }
    }


    // ── LLM generation ──

    private async Task<List<string>> GenerateForNpc(string npcType, string gameDate,
        string phase, List<EventSignal> signals, int? entityId, SqliteConnection conn)
    {
        if (!Personalities.TryGetValue(npcType, out var personality))
            return [];

        var factSheet = BuildFactSheet(conn, npcType, gameDate, phase, signals, entityId);

        var systemPrompt = $"""
            {personality.SystemPrompt.Trim()}

            CRITICAL RULES:
            - ONLY reference facts, data, and events from the FACT SHEET below.
            - Do NOT invent any prices, percentages, dates, or statistics not in the fact sheet.
            - Keep each line to 1-2 sentences. Short and punchy — this is game dialogue, not a report.
            - Stay in character. Do not break the fourth wall.
            - Speak directly to the player as if they walked up to you on the trading floor.

            Respond with a JSON array of dialogue lines. Each line is a short sentence or two that
            the player will click through one at a time. Generate 2-4 lines that form a natural
            conversational flow.

            Example format:
            ["Hey, did you see AAPL today? Down 3%.", "The whole tech sector is getting hit.", "Might be worth watching for a bounce tomorrow."]

            Respond with ONLY the JSON array. No markdown, no explanation.
            """;

        var userPrompt = $"""
            FACT SHEET:
            {factSheet}

            Generate {personality.DisplayName}'s dialogue for this moment.
            """;

        try
        {
            var requestBody = new
            {
                model = _model,
                max_tokens = 512,
                system = systemPrompt,
                messages = new[]
                {
                    new { role = "user", content = userPrompt }
                }
            };

            var jsonRequest = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Dialogue] Claude API error for {npcType}: {response.StatusCode}");
                return [];
            }

            using var doc = JsonDocument.Parse(responseJson);
            var textBlock = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            if (textBlock is null) return [];

            var cleaned = textBlock.Trim();
            if (cleaned.StartsWith("```"))
            {
                cleaned = cleaned.Split('\n', 2).Length > 1 ? cleaned.Split('\n', 2)[1] : cleaned;
                if (cleaned.EndsWith("```"))
                    cleaned = cleaned[..^3];
                cleaned = cleaned.Trim();
            }

            var lines = JsonSerializer.Deserialize<List<string>>(cleaned);
            return lines ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Dialogue] Generation failed for {npcType}: {ex.Message}");
            return [];
        }
    }

    // ── Caching ──

    private bool HasCachedDialogue(SqliteConnection conn, string gameDate, string phase, int? entityId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM dynamic_npc_dialogue WHERE date = @d AND phase = @p;";
        cmd.Parameters.AddWithValue("@d", gameDate);
        cmd.Parameters.AddWithValue("@p", phase);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private void CacheDynamicLines(SqliteConnection conn, string gameDate, string phase,
        string npcType, List<string> lines, List<EventSignal> signals, int? entityId)
    {
        var triggerSource = signals.Count > 0 ? signals[0].Type : null;
        var priority = signals.Any(s => s.Weight >= 0.15) ? "high" : "medium";

        for (int i = 0; i < lines.Count; i++)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO dynamic_npc_dialogue (date, npc_type, category, text, phase, priority, trigger_source, entity_id, line_order)
                VALUES (@d, @npc, @cat, @text, @phase, @pri, @trigger, @eid, @order);
                """;
            cmd.Parameters.AddWithValue("@d", gameDate);
            cmd.Parameters.AddWithValue("@npc", npcType);
            cmd.Parameters.AddWithValue("@cat", "generated");
            cmd.Parameters.AddWithValue("@text", lines[i]);
            cmd.Parameters.AddWithValue("@phase", phase);
            cmd.Parameters.AddWithValue("@pri", priority);
            cmd.Parameters.AddWithValue("@trigger", (object?)triggerSource ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@eid", (object?)entityId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@order", i);
            cmd.ExecuteNonQuery();
        }
    }

    private int SelectAndCacheStaticLines(SqliteConnection conn, string gameDate,
        string phase, string mood, int count)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, npc_type, text, line_order FROM static_npc_dialogue
            WHERE date IS NULL
              AND (phase IS NULL OR phase = @phase)
              AND (mood IS NULL OR mood = @mood)
            ORDER BY RANDOM()
            LIMIT @n;
            """;
        cmd.Parameters.AddWithValue("@phase", phase);
        cmd.Parameters.AddWithValue("@mood", mood);
        cmd.Parameters.AddWithValue("@n", count);

        var selected = new List<(string NpcType, string Text, int LineOrder)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            selected.Add((
                reader.IsDBNull(1) ? "trader" : reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
            ));
        }

        foreach (var (npcType, text, lineOrder) in selected)
        {
            using var insert = conn.CreateCommand();
            insert.CommandText = """
                INSERT INTO dynamic_npc_dialogue (date, npc_type, category, text, phase, priority, line_order)
                VALUES (@d, @npc, 'ambient', @text, @phase, 'low', @order);
                """;
            insert.Parameters.AddWithValue("@d", gameDate);
            insert.Parameters.AddWithValue("@npc", npcType);
            insert.Parameters.AddWithValue("@text", text);
            insert.Parameters.AddWithValue("@phase", phase);
            insert.Parameters.AddWithValue("@order", lineOrder);
            insert.ExecuteNonQuery();
        }

        return selected.Count;
    }

    // ── Helpers ──

    private string? GetArcTone(string gameDate)
    {
        if (_arcService is null) return null;
        var allArcs = _arcService.GetAllArcs();
        var arc = allArcs.FirstOrDefault(a =>
            string.Compare(gameDate, a.StartDate, StringComparison.Ordinal) >= 0 &&
            string.Compare(gameDate, a.EndDate, StringComparison.Ordinal) <= 0);
        return arc?.NewspaperTone;
    }

    private static string DeriveMarketMood(List<EventSignal> signals)
    {
        if (signals.Any(s => s.Type is "broad_selloff" or "market_wide_decline"))
            return "bearish";
        if (signals.Any(s => s.Type is "broad_rally"))
            return "bullish";
        return "neutral";
    }

    private record NpcPersonality(string DisplayName, string SystemPrompt);
}

public record EventSignal(string Type, string Description, double Weight);
