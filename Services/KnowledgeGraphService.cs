using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using TradingGame.Data;
using TradingGame.Models;

namespace TradingGame.Services;

public class KnowledgeGraphService
{
    private readonly Database _db;
    private readonly KnowledgeGraphConfig _config;

    public event Action<int, string, string, string?, string?>? OnNodeUnlocked;

    public KnowledgeGraphService(Database db, string configPath)
    {
        _db = db;
        var json = File.ReadAllText(configPath);
        _config = JsonSerializer.Deserialize<KnowledgeGraphConfig>(json)!;
    }

    public KnowledgeGraphConfig Config => _config;

    // ── Get full graph state for a player ──

    public KnowledgeGraphResponse GetGraph(int entityId)
    {
        using var conn = _db.Open();
        var progress = LoadProgress(conn, entityId);

        var nodes = _config.Nodes.Select(n =>
        {
            var status = progress.TryGetValue(n.Id, out var p) ? p.Status : "locked";
            var unlockedAt = progress.TryGetValue(n.Id, out var p2) ? p2.UnlockedAt : null;
            var completedAt = progress.TryGetValue(n.Id, out var p3) ? p3.CompletedAt : null;

            return new KnowledgeNodeStateDto(
                n.Id, n.Title, n.Type, n.Description,
                status != "locked" ? n.Content : null,
                n.Prerequisites, n.Category, n.Priority,
                new NodePositionDto(n.Position.X, n.Position.Y),
                status, unlockedAt, completedAt,
                n.Reward?.Mechanic,
                n.TriggerExplanation,
                n.CorrectAction
            );
        }).ToList();

        return new KnowledgeGraphResponse("ok", nodes, _config.Categories);
    }

    // ── Mark a knowledge node as completed (after cutscene) ──

    public KnowledgeNodeUpdateResponse CompleteNode(int entityId, string nodeId, string gameDate)
    {
        var node = _config.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null)
            return new KnowledgeNodeUpdateResponse("error", nodeId, null, "Node not found");

        using var conn = _db.Open();
        var progress = LoadProgress(conn, entityId);

        if (!progress.TryGetValue(nodeId, out var current) || current.Status == "locked")
            return new KnowledgeNodeUpdateResponse("error", nodeId, null, "Node is not unlocked yet");

        if (current.Status == "completed")
            return new KnowledgeNodeUpdateResponse("ok", nodeId, "completed", "Already completed");

        SetProgress(conn, entityId, nodeId, "completed", current.UnlockedAt, gameDate);

        var newlyUnlocked = UnlockDependents(conn, entityId, nodeId, progress, gameDate);

        return new KnowledgeNodeUpdateResponse("ok", nodeId, "completed", null)
        {
            NewlyUnlocked = newlyUnlocked,
            RewardMechanic = node.Reward?.Mechanic
        };
    }

    // ── Unlock initial knowledge nodes for a new player ──

    public List<string> InitializeForEntity(int entityId, string gameDate)
    {
        using var conn = _db.Open();
        var progress = LoadProgress(conn, entityId);
        var unlocked = new List<string>();

        foreach (var node in _config.Nodes.Where(n => n.Type == "knowledge" && n.Prerequisites.Count == 0))
        {
            if (progress.ContainsKey(node.Id))
                continue;

            SetProgress(conn, entityId, node.Id, "unlocked", gameDate, null);
            unlocked.Add(node.Id);
        }

        return unlocked;
    }

    // ── Get all mechanics unlocked by completed nodes ──

    public List<string> GetUnlockedMechanics(int entityId)
    {
        using var conn = _db.Open();
        var progress = LoadProgress(conn, entityId);

        return _config.Nodes
            .Where(n => n.Reward is not null
                && progress.TryGetValue(n.Id, out var p)
                && p.Status == "completed")
            .Select(n => n.Reward!.Mechanic)
            .ToList();
    }

    // ── Evaluate adaptive triggers (called after trades/day advance) ──
    // Supports dual-path triggers: evaluates negative/positive/time paths, first-wins.

    public List<UnlockedNodeDto> EvaluateTriggers(int entityId, string gameDate)
    {
        using var conn = _db.Open();
        var progress = LoadProgress(conn, entityId);
        var newlyUnlocked = new List<UnlockedNodeDto>();

        var adaptiveNodes = _config.Nodes
            .Where(n => n.Type == "adaptive" && (n.Trigger is not null || n.Triggers is { Count: > 0 }))
            .Where(n => !progress.ContainsKey(n.Id) || progress[n.Id].Status == "locked");

        foreach (var node in adaptiveNodes)
        {
            if (!PrerequisitesMet(node, progress))
                continue;

            if (node.Triggers is { Count: > 0 })
            {
                foreach (var path in node.Triggers)
                {
                    var triggerConfig = new TriggerConfig { Type = path.Type, Params = path.Params };
                    if (CheckTrigger(conn, entityId, triggerConfig, gameDate))
                    {
                        SetProgress(conn, entityId, node.Id, "unlocked", gameDate, null);
                        newlyUnlocked.Add(new UnlockedNodeDto(node.Id, node.Title, node.Priority, node.Category));
                        var explanation = path.TriggerExplanation ?? node.TriggerExplanation;
                        OnNodeUnlocked?.Invoke(entityId, node.Id, gameDate, explanation, path.Path);
                        break;
                    }
                }
            }
            else if (node.Trigger is not null && CheckTrigger(conn, entityId, node.Trigger, gameDate))
            {
                SetProgress(conn, entityId, node.Id, "unlocked", gameDate, null);
                newlyUnlocked.Add(new UnlockedNodeDto(node.Id, node.Title, node.Priority, node.Category));
                OnNodeUnlocked?.Invoke(entityId, node.Id, gameDate, node.TriggerExplanation, null);
            }
        }

        return newlyUnlocked;
    }

    // ── Trigger evaluation logic ──

    private bool CheckTrigger(SqliteConnection conn, int entityId, TriggerConfig trigger, string gameDate)
    {
        return trigger.Type switch
        {
            "trade_count" => CheckTradeCount(conn, entityId, trigger.Params),
            "held_stock_daily_change" => CheckHeldStockChange(conn, entityId, gameDate, trigger.Params),
            "portfolio_concentration" => CheckConcentration(conn, entityId, trigger.Params),
            "first_profitable_sell" => CheckFirstProfitableSell(conn, entityId),
            "first_losing_sell" => CheckFirstLosingSell(conn, entityId),
            "same_ticker_multiple_buys" => CheckMultipleBuys(conn, entityId, trigger.Params),
            "unrealized_gain_pct" => CheckUnrealizedGain(conn, entityId, gameDate, trigger.Params),
            "unrealized_loss_pct" => CheckUnrealizedLoss(conn, entityId, gameDate, trigger.Params),
            "bought_after_decline" => CheckBoughtDip(conn, entityId, gameDate, trigger.Params),
            "low_cash_ratio" => CheckLowCash(conn, entityId, gameDate, trigger.Params),
            "market_wide_decline" => CheckMarketDecline(conn, gameDate, trigger.Params),
            "traded_both_phases" => CheckTradedBothPhases(conn, entityId),
            "held_overnight_gap" => CheckHeldOvernightGap(conn, entityId, gameDate, trigger.Params),
            "maintained_diversification" => CheckMaintainedDiversification(conn, entityId, gameDate, trigger.Params),
            "maintained_cash_reserves" => CheckMaintainedCashReserves(conn, entityId, gameDate, trigger.Params),
            "trading_days_elapsed" => CheckTradingDaysElapsed(conn, entityId, trigger.Params),
            "newspaper_read" => CheckNewspaperRead(conn, entityId, trigger.Params),
            "npc_interaction" => CheckNpcInteraction(conn, entityId, trigger.Params),
            _ => false
        };
    }

    private bool CheckTradeCount(SqliteConnection conn, int entityId, Dictionary<string, JsonElement> p)
    {
        var minTrades = p.TryGetValue("min_trades", out var mt) ? mt.GetInt32() : 3;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM trade_history WHERE entity_id = @eid;";
        cmd.Parameters.AddWithValue("@eid", entityId);
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        return count >= minTrades;
    }

    private bool CheckHeldStockChange(SqliteConnection conn, int entityId, string gameDate, Dictionary<string, JsonElement> p)
    {
        var threshold = p.TryGetValue("change_pct", out var cp) ? cp.GetDouble() : 5.0;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT tp.ticker_id,
                   ABS((tp.close_price - prev.close_price) / prev.close_price * 100) as pct_change
            FROM portfolio pf
            JOIN ticker_prices tp ON tp.ticker_id = pf.ticker_id AND tp.date = @date
            JOIN ticker_prices prev ON prev.ticker_id = pf.ticker_id
                AND prev.date = (SELECT MAX(date) FROM ticker_prices WHERE ticker_id = pf.ticker_id AND date < @date)
            WHERE pf.entity_id = @eid AND pf.shares_held > 0
            """;
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@date", gameDate);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (!reader.IsDBNull(1) && reader.GetDouble(1) >= threshold)
                return true;
        }
        return false;
    }

    private bool CheckConcentration(SqliteConnection conn, int entityId, Dictionary<string, JsonElement> p)
    {
        var maxPct = p.TryGetValue("max_single_ticker_pct", out var mc) ? mc.GetDouble() : 80.0;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT ticker_id, SUM(shares_held * price) as value
            FROM portfolio WHERE entity_id = @eid AND shares_held > 0
            GROUP BY ticker_id;
            """;
        cmd.Parameters.AddWithValue("@eid", entityId);

        var values = new List<double>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            values.Add(reader.GetDouble(1));

        if (values.Count < 1) return false;
        var total = values.Sum();
        if (total <= 0) return false;
        var maxConc = values.Max() / total * 100.0;
        return maxConc >= maxPct;
    }

    private bool CheckFirstProfitableSell(SqliteConnection conn, int entityId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM trade_history th
            WHERE th.entity_id = @eid AND th.shares < 0
            AND th.price_paid > (
                SELECT AVG(th2.price_paid) FROM trade_history th2
                WHERE th2.entity_id = @eid AND th2.ticker_id = th.ticker_id AND th2.shares > 0
                AND th2.trade_date <= th.trade_date
            );
            """;
        cmd.Parameters.AddWithValue("@eid", entityId);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private bool CheckFirstLosingSell(SqliteConnection conn, int entityId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM trade_history th
            WHERE th.entity_id = @eid AND th.shares < 0
            AND th.price_paid < (
                SELECT AVG(th2.price_paid) FROM trade_history th2
                WHERE th2.entity_id = @eid AND th2.ticker_id = th.ticker_id AND th2.shares > 0
                AND th2.trade_date <= th.trade_date
            );
            """;
        cmd.Parameters.AddWithValue("@eid", entityId);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private bool CheckMultipleBuys(SqliteConnection conn, int entityId, Dictionary<string, JsonElement> p)
    {
        var minBuys = p.TryGetValue("min_buys", out var mb) ? mb.GetInt32() : 2;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT ticker_id, COUNT(*) as buy_count, COUNT(DISTINCT ROUND(price_paid, 2)) as distinct_prices
            FROM trade_history
            WHERE entity_id = @eid AND shares > 0
            GROUP BY ticker_id
            HAVING buy_count >= @min AND distinct_prices >= 2;
            """;
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@min", minBuys);

        using var reader = cmd.ExecuteReader();
        return reader.Read();
    }

    private bool CheckUnrealizedGain(SqliteConnection conn, int entityId, string gameDate, Dictionary<string, JsonElement> p)
    {
        var gainPct = p.TryGetValue("gain_pct", out var gp) ? gp.GetDouble() : 20.0;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT pf.ticker_id, pf.price as purchase_price, tp.close_price
            FROM portfolio pf
            JOIN ticker_prices tp ON tp.ticker_id = pf.ticker_id AND tp.date = @date
            WHERE pf.entity_id = @eid AND pf.shares_held > 0;
            """;
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@date", gameDate);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var purchasePrice = reader.GetDouble(1);
            var currentPrice = reader.GetDouble(2);
            if (purchasePrice > 0 && (currentPrice - purchasePrice) / purchasePrice * 100 >= gainPct)
                return true;
        }
        return false;
    }

    private bool CheckUnrealizedLoss(SqliteConnection conn, int entityId, string gameDate, Dictionary<string, JsonElement> p)
    {
        var lossPct = p.TryGetValue("loss_pct", out var lp) ? lp.GetDouble() : 15.0;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT pf.ticker_id, pf.price as purchase_price, tp.close_price
            FROM portfolio pf
            JOIN ticker_prices tp ON tp.ticker_id = pf.ticker_id AND tp.date = @date
            WHERE pf.entity_id = @eid AND pf.shares_held > 0;
            """;
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@date", gameDate);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var purchasePrice = reader.GetDouble(1);
            var currentPrice = reader.GetDouble(2);
            if (purchasePrice > 0 && (purchasePrice - currentPrice) / purchasePrice * 100 >= lossPct)
                return true;
        }
        return false;
    }

    private bool CheckBoughtDip(SqliteConnection conn, int entityId, string gameDate, Dictionary<string, JsonElement> p)
    {
        var declinePct = p.TryGetValue("decline_pct", out var dp) ? dp.GetDouble() : 5.0;
        var lookback = p.TryGetValue("lookback_days", out var lb) ? lb.GetInt32() : 5;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT th.ticker_id, th.price_paid,
                   (SELECT MAX(tp.close_price) FROM ticker_prices tp
                    WHERE tp.ticker_id = th.ticker_id
                    AND tp.date >= (SELECT date FROM ticker_prices WHERE ticker_id = th.ticker_id AND date <= @date ORDER BY date DESC LIMIT 1 OFFSET @lb)
                    AND tp.date < @date) as recent_high
            FROM trade_history th
            WHERE th.entity_id = @eid AND th.shares > 0 AND th.trade_date = @date;
            """;
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@date", gameDate);
        cmd.Parameters.AddWithValue("@lb", lookback);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader.IsDBNull(2)) continue;
            var recentHigh = reader.GetDouble(2);
            var buyPrice = reader.GetDouble(1);
            if (recentHigh > 0 && (recentHigh - buyPrice) / recentHigh * 100 >= declinePct)
                return true;
        }
        return false;
    }

    private bool CheckLowCash(SqliteConnection conn, int entityId, string gameDate, Dictionary<string, JsonElement> p)
    {
        var threshold = p.TryGetValue("cash_pct_below", out var cb) ? cb.GetDouble() : 10.0;

        using var cashCmd = conn.CreateCommand();
        cashCmd.CommandText = "SELECT available_cash FROM entity WHERE entity_id = @eid;";
        cashCmd.Parameters.AddWithValue("@eid", entityId);
        var cash = Convert.ToDouble(cashCmd.ExecuteScalar() ?? 0);

        using var posCmd = conn.CreateCommand();
        posCmd.CommandText = """
            SELECT COALESCE(SUM(pf.shares_held * tp.close_price), 0)
            FROM portfolio pf
            JOIN ticker_prices tp ON tp.ticker_id = pf.ticker_id AND tp.date = @date
            WHERE pf.entity_id = @eid AND pf.shares_held > 0;
            """;
        posCmd.Parameters.AddWithValue("@eid", entityId);
        posCmd.Parameters.AddWithValue("@date", gameDate);
        var positionValue = Convert.ToDouble(posCmd.ExecuteScalar() ?? 0);

        var total = cash + positionValue;
        if (total <= 0) return false;
        return (cash / total * 100) < threshold;
    }

    private bool CheckMarketDecline(SqliteConnection conn, string gameDate, Dictionary<string, JsonElement> p)
    {
        var threshold = p.TryGetValue("avg_decline_pct", out var ad) ? ad.GetDouble() : 3.0;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT AVG((tp.close_price - prev.close_price) / prev.close_price * 100) as avg_change
            FROM ticker_prices tp
            JOIN ticker_prices prev ON prev.ticker_id = tp.ticker_id
                AND prev.date = (SELECT MAX(date) FROM ticker_prices WHERE ticker_id = tp.ticker_id AND date < @date)
            WHERE tp.date = @date;
            """;
        cmd.Parameters.AddWithValue("@date", gameDate);

        var result = cmd.ExecuteScalar();
        if (result is null or DBNull) return false;
        return Convert.ToDouble(result) <= -threshold;
    }

    private bool CheckTradedBothPhases(SqliteConnection conn, int entityId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(DISTINCT trade_phase) FROM trade_history
            WHERE entity_id = @eid AND trade_phase IS NOT NULL;
            """;
        cmd.Parameters.AddWithValue("@eid", entityId);
        return Convert.ToInt32(cmd.ExecuteScalar()) >= 2;
    }

    private bool CheckHeldOvernightGap(SqliteConnection conn, int entityId, string gameDate, Dictionary<string, JsonElement> p)
    {
        var threshold = p.TryGetValue("gap_pct", out var gp) ? gp.GetDouble() : 1.0;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT ABS((tp_today.open_price - tp_prev.close_price) / tp_prev.close_price * 100) as gap_pct
            FROM portfolio pf
            JOIN ticker_prices tp_today ON tp_today.ticker_id = pf.ticker_id AND tp_today.date = @date
            JOIN ticker_prices tp_prev ON tp_prev.ticker_id = pf.ticker_id
                AND tp_prev.date = (SELECT MAX(date) FROM ticker_prices WHERE ticker_id = pf.ticker_id AND date < @date)
            WHERE pf.entity_id = @eid AND pf.shares_held > 0
                AND tp_prev.close_price > 0
            """;
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@date", gameDate);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0) && reader.GetDouble(0) >= threshold)
                return true;
        }
        return false;
    }

    // ── Positive path triggers (dual-path system) ──

    private bool CheckMaintainedDiversification(SqliteConnection conn, int entityId, string gameDate, Dictionary<string, JsonElement> p)
    {
        var minTickers = p.TryGetValue("min_tickers", out var mt) ? mt.GetInt32() : 4;
        var minDays = p.TryGetValue("min_days", out var md) ? md.GetInt32() : 5;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(DISTINCT th.trade_date) as trading_days
            FROM trade_history th
            WHERE th.entity_id = @eid AND th.trade_date <= @date;
            """;
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@date", gameDate);
        var totalDays = Convert.ToInt32(cmd.ExecuteScalar());
        if (totalDays < minDays) return false;

        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = """
            SELECT COUNT(DISTINCT ticker_id) FROM portfolio
            WHERE entity_id = @eid AND shares_held > 0;
            """;
        cmd2.Parameters.AddWithValue("@eid", entityId);
        var currentTickers = Convert.ToInt32(cmd2.ExecuteScalar());

        return currentTickers >= minTickers;
    }

    private bool CheckMaintainedCashReserves(SqliteConnection conn, int entityId, string gameDate, Dictionary<string, JsonElement> p)
    {
        var minCashPct = p.TryGetValue("min_cash_pct", out var mc) ? mc.GetDouble() : 20.0;
        var minDays = p.TryGetValue("min_days", out var md) ? md.GetInt32() : 5;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM net_worth_history
            WHERE entity_id = @eid AND net_worth > 0
              AND (cash / net_worth * 100) >= @pct;
            """;
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@pct", minCashPct);
        var daysAbove = Convert.ToInt32(cmd.ExecuteScalar());

        return daysAbove >= minDays;
    }

    private bool CheckTradingDaysElapsed(SqliteConnection conn, int entityId, Dictionary<string, JsonElement> p)
    {
        var minDays = p.TryGetValue("min_days", out var md) ? md.GetInt32() : 10;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(DISTINCT trade_date) FROM trade_history WHERE entity_id = @eid;";
        cmd.Parameters.AddWithValue("@eid", entityId);
        var days = Convert.ToInt32(cmd.ExecuteScalar());

        return days >= minDays;
    }

    // ── Player event triggers (newspaper_read, npc_interaction) ──

    private bool CheckNewspaperRead(SqliteConnection conn, int entityId, Dictionary<string, JsonElement> p)
    {
        var minReads = p.TryGetValue("min_reads", out var mr) ? mr.GetInt32() : 3;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM player_events
            WHERE entity_id = @eid AND event_type = 'newspaper_read';
            """;
        cmd.Parameters.AddWithValue("@eid", entityId);
        var reads = Convert.ToInt32(cmd.ExecuteScalar());

        return reads >= minReads;
    }

    private bool CheckNpcInteraction(SqliteConnection conn, int entityId, Dictionary<string, JsonElement> p)
    {
        var minInteractions = p.TryGetValue("min_interactions", out var mi) ? mi.GetInt32() : 3;
        var npcType = p.TryGetValue("npc_type", out var nt) ? nt.GetString() : null;

        using var cmd = conn.CreateCommand();
        if (npcType is not null)
        {
            cmd.CommandText = """
                SELECT COUNT(*) FROM player_events
                WHERE entity_id = @eid AND event_type = 'npc_interaction'
                  AND json_extract(metadata_json, '$.npc_type') = @npc;
                """;
            cmd.Parameters.AddWithValue("@npc", npcType);
        }
        else
        {
            cmd.CommandText = """
                SELECT COUNT(*) FROM player_events
                WHERE entity_id = @eid AND event_type = 'npc_interaction';
                """;
        }
        cmd.Parameters.AddWithValue("@eid", entityId);
        var count = Convert.ToInt32(cmd.ExecuteScalar());

        return count >= minInteractions;
    }

    // ── NPC Quest system ──

    public NpcQuestCompleteResponse CompleteNpcQuest(int entityId, string npcType, string gameDate)
    {
        var quest = _config.NpcQuests?.FirstOrDefault(q => q.NpcType == npcType);
        if (quest is null)
            return new NpcQuestCompleteResponse("error", npcType, null, "Unknown NPC type");

        using var conn = _db.Open();
        var progress = LoadProgress(conn, entityId);
        var unlocked = new List<string>();

        foreach (var nodeId in quest.UnlocksNodes)
        {
            var node = _config.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node is null) continue;
            if (progress.TryGetValue(nodeId, out var p) && p.Status != "locked") continue;
            if (!PrerequisitesMet(node, progress)) continue;

            SetProgress(conn, entityId, nodeId, "unlocked", gameDate, null);
            progress[nodeId] = new NodeProgress("unlocked", gameDate, null);
            unlocked.Add(nodeId);
            OnNodeUnlocked?.Invoke(entityId, nodeId, gameDate,
                $"Unlocked through conversation with {quest.Name}.", "quest");
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO npc_quest_progress (entity_id, npc_type, node_id, status, completed_at)
            VALUES (@eid, @npc, @nid, 'completed', @date)
            ON CONFLICT(entity_id, npc_type, node_id) DO UPDATE SET
                status = 'completed', completed_at = excluded.completed_at;
            """;
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@npc", npcType);
        cmd.Parameters.AddWithValue("@date", gameDate);

        foreach (var nodeId in unlocked)
        {
            cmd.Parameters["@nid"].Value = nodeId;
            cmd.ExecuteNonQuery();
        }

        return new NpcQuestCompleteResponse("ok", npcType, unlocked.Count > 0 ? unlocked : null, null);
    }

    // ── Player event recording ──

    public void RecordPlayerEvent(int entityId, string eventType, string eventDate, string? metadataJson)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO player_events (entity_id, event_type, event_date, metadata_json, created_at)
            VALUES (@eid, @type, @date, @meta, @now)
            ON CONFLICT(entity_id, event_type, event_date) DO NOTHING;
            """;
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@type", eventType);
        cmd.Parameters.AddWithValue("@date", eventDate);
        cmd.Parameters.AddWithValue("@meta", (object?)metadataJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    // ── Helper methods ──

    private bool PrerequisitesMet(KnowledgeNodeConfig node, Dictionary<string, NodeProgress> progress)
    {
        return node.Prerequisites.All(prereq =>
            progress.TryGetValue(prereq, out var p) && p.Status == "completed");
    }

    private List<string> UnlockDependents(SqliteConnection conn, int entityId, string completedNodeId,
        Dictionary<string, NodeProgress> progress, string gameDate)
    {
        var unlocked = new List<string>();
        progress[completedNodeId] = new NodeProgress("completed", gameDate, gameDate);

        foreach (var node in _config.Nodes.Where(n => n.Prerequisites.Contains(completedNodeId)))
        {
            if (progress.ContainsKey(node.Id) && progress[node.Id].Status != "locked")
                continue;

            if (PrerequisitesMet(node, progress) && node.Type == "knowledge")
            {
                SetProgress(conn, entityId, node.Id, "unlocked", gameDate, null);
                unlocked.Add(node.Id);
            }
        }

        return unlocked;
    }

    private Dictionary<string, NodeProgress> LoadProgress(SqliteConnection conn, int entityId)
    {
        var dict = new Dictionary<string, NodeProgress>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT node_id, status, unlocked_at, completed_at FROM knowledge_node_progress WHERE entity_id = @eid;";
        cmd.Parameters.AddWithValue("@eid", entityId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            dict[reader.GetString(0)] = new NodeProgress(
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)
            );
        }
        return dict;
    }

    private void SetProgress(SqliteConnection conn, int entityId, string nodeId,
        string status, string? unlockedAt, string? completedAt)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO knowledge_node_progress (entity_id, node_id, status, unlocked_at, completed_at)
            VALUES (@eid, @nid, @status, @unlocked, @completed)
            ON CONFLICT(entity_id, node_id) DO UPDATE SET
                status = excluded.status,
                unlocked_at = COALESCE(excluded.unlocked_at, knowledge_node_progress.unlocked_at),
                completed_at = COALESCE(excluded.completed_at, knowledge_node_progress.completed_at);
            """;
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@nid", nodeId);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@unlocked", (object?)unlockedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@completed", (object?)completedAt ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private record NodeProgress(string Status, string? UnlockedAt, string? CompletedAt);
}

// ── JSON config model (deserialized from knowledge_graph.json) ──

public class KnowledgeGraphConfig
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("nodes")]
    public List<KnowledgeNodeConfig> Nodes { get; set; } = [];

    [JsonPropertyName("categories")]
    public Dictionary<string, CategoryConfig> Categories { get; set; } = new();

    [JsonPropertyName("npc_quests")]
    public List<NpcQuestConfig>? NpcQuests { get; set; }

    [JsonPropertyName("settings")]
    public GraphSettings Settings { get; set; } = new();
}

public class KnowledgeNodeConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "knowledge";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("prerequisites")]
    public List<string> Prerequisites { get; set; } = [];

    [JsonPropertyName("trigger")]
    public TriggerConfig? Trigger { get; set; }

    [JsonPropertyName("triggers")]
    public List<TriggerPathConfig>? Triggers { get; set; }

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "normal";

    [JsonPropertyName("position")]
    public NodePosition Position { get; set; } = new();

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("reward")]
    public RewardConfig? Reward { get; set; }

    [JsonPropertyName("trigger_explanation")]
    public string? TriggerExplanation { get; set; }

    [JsonPropertyName("correct_action")]
    public string? CorrectAction { get; set; }
}

public class RewardConfig
{
    [JsonPropertyName("mechanic")]
    public string Mechanic { get; set; } = "";
}

public class TriggerConfig
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("params")]
    public Dictionary<string, JsonElement> Params { get; set; } = new();
}

public class TriggerPathConfig
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "negative";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("params")]
    public Dictionary<string, JsonElement> Params { get; set; } = new();

    [JsonPropertyName("trigger_explanation")]
    public string? TriggerExplanation { get; set; }
}

public class NpcQuestConfig
{
    [JsonPropertyName("npc_type")]
    public string NpcType { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("unlocks_nodes")]
    public List<string> UnlocksNodes { get; set; } = [];

    [JsonPropertyName("quest_dialogue_hint")]
    public string? QuestDialogueHint { get; set; }
}

public record NpcQuestCompleteResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("npc_type")] string NpcType,
    [property: JsonPropertyName("unlocked_nodes")] List<string>? UnlockedNodes,
    [property: JsonPropertyName("message")] string? Message
);

public class NodePosition
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }
}

public class CategoryConfig
{
    [JsonPropertyName("color")]
    public string Color { get; set; } = "#FFFFFF";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";
}

public class GraphSettings
{
    [JsonPropertyName("unlock_all_knowledge_first")]
    public bool UnlockAllKnowledgeFirst { get; set; } = true;

    [JsonPropertyName("max_popups_per_day")]
    public int MaxPopupsPerDay { get; set; } = 2;
}
