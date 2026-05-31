using System.Text.Json.Serialization;

namespace TradingGame.Models;

public record HoldingSnapshot(
    [property: JsonPropertyName("ticker_id")] string TickerId,
    [property: JsonPropertyName("shares_held")] double SharesHeld,
    [property: JsonPropertyName("avg_cost_basis")] double AvgCostBasis,
    [property: JsonPropertyName("current_price")] double CurrentPrice,
    [property: JsonPropertyName("market_value")] double MarketValue,
    [property: JsonPropertyName("unrealized_pnl_pct")] double UnrealizedPnlPct
);

public record PortfolioSnapshot(
    [property: JsonPropertyName("holdings")] List<HoldingSnapshot> Holdings,
    [property: JsonPropertyName("total_holdings_value")] double TotalHoldingsValue,
    [property: JsonPropertyName("cash")] double Cash,
    [property: JsonPropertyName("net_worth")] double NetWorth,
    [property: JsonPropertyName("cash_ratio_pct")] double CashRatioPct
);

public record ConcentrationSnapshot(
    [property: JsonPropertyName("max_single_ticker_pct")] double MaxSingleTickerPct,
    [property: JsonPropertyName("max_ticker")] string? MaxTicker,
    [property: JsonPropertyName("position_count")] int PositionCount
);

public record KnowledgeNodeSummary(
    [property: JsonPropertyName("node_id")] string NodeId,
    [property: JsonPropertyName("title")] string Title
);

public record KnowledgeSnapshot(
    [property: JsonPropertyName("completed_nodes")] List<KnowledgeNodeSummary> CompletedNodes,
    [property: JsonPropertyName("unlocked_not_completed")] List<KnowledgeNodeSummary> UnlockedNotCompleted,
    [property: JsonPropertyName("total_nodes")] int TotalNodes,
    [property: JsonPropertyName("completed_count")] int CompletedCount,
    [property: JsonPropertyName("recently_unlocked")] List<KnowledgeNodeSummary> RecentlyUnlocked
);

public record TradeSnapshot(
    [property: JsonPropertyName("ticker_id")] string TickerId,
    [property: JsonPropertyName("shares")] double Shares,
    [property: JsonPropertyName("price")] double Price,
    [property: JsonPropertyName("trade_date")] string TradeDate
);

public record RecentTradesSnapshot(
    [property: JsonPropertyName("todays_trades")] List<TradeSnapshot> TodaysTrades,
    [property: JsonPropertyName("last_n_trades")] List<TradeSnapshot> LastNTrades,
    [property: JsonPropertyName("total_trade_count")] int TotalTradeCount
);

public record ArcSnapshot(
    [property: JsonPropertyName("arc_name")] string? ArcName,
    [property: JsonPropertyName("days_remaining")] int? DaysRemaining,
    [property: JsonPropertyName("projected_return_pct")] double? ProjectedReturnPct,
    [property: JsonPropertyName("projected_grade")] string? ProjectedGrade,
    [property: JsonPropertyName("arc_tone")] string? ArcTone
);

public record MarketMoverSnapshot(
    [property: JsonPropertyName("ticker_id")] string TickerId,
    [property: JsonPropertyName("close_price")] double ClosePrice,
    [property: JsonPropertyName("change_pct")] double ChangePct
);

public record MarketSnapshot(
    [property: JsonPropertyName("current_date")] string CurrentDate,
    [property: JsonPropertyName("phase")] string Phase,
    [property: JsonPropertyName("top_gainers")] List<MarketMoverSnapshot> TopGainers,
    [property: JsonPropertyName("top_losers")] List<MarketMoverSnapshot> TopLosers,
    [property: JsonPropertyName("advancing_count")] int AdvancingCount,
    [property: JsonPropertyName("declining_count")] int DecliningCount,
    [property: JsonPropertyName("total_count")] int TotalCount
);

public record PlayerContext(
    [property: JsonPropertyName("entity_id")] int EntityId,
    [property: JsonPropertyName("game_date")] string GameDate,
    [property: JsonPropertyName("game_phase")] string GamePhase,
    [property: JsonPropertyName("portfolio")] PortfolioSnapshot Portfolio,
    [property: JsonPropertyName("concentration")] ConcentrationSnapshot Concentration,
    [property: JsonPropertyName("knowledge")] KnowledgeSnapshot Knowledge,
    [property: JsonPropertyName("recent_trades")] RecentTradesSnapshot RecentTrades,
    [property: JsonPropertyName("arc")] ArcSnapshot Arc,
    [property: JsonPropertyName("market")] MarketSnapshot Market
);
