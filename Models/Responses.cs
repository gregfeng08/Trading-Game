using System.Text.Json.Serialization;
using TradingGame.Services;

namespace TradingGame.Models;

// ── System ──

public record PingResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("server_time")] double ServerTime
);

public record StatusResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("server_time")] double ServerTime,
    [property: JsonPropertyName("uptime_seconds")] double UptimeSeconds,
    [property: JsonPropertyName("db_connected")] bool DbConnected,
    [property: JsonPropertyName("version")] string Version
);

public record InitDbResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("initialized")] bool Initialized
);

public record LoadTickerDataResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("ticker_count")] int TickerCount
);

public record DbResetResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message
);

// ── Market ──

public record TickerDto(
    [property: JsonPropertyName("ticker_id")] string TickerId,
    [property: JsonPropertyName("company_name")] string? CompanyName,
    [property: JsonPropertyName("description")] string? Description
);

public record TickerListResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("tickers")] List<TickerDto> Tickers
);

public record PriceRowDto(
    [property: JsonPropertyName("ticker_id")] string TickerId,
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("open_price")] double OpenPrice,
    [property: JsonPropertyName("high_price")] double HighPrice,
    [property: JsonPropertyName("low_price")] double LowPrice,
    [property: JsonPropertyName("close_price")] double ClosePrice
);

public record PricesResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("ticker_id")] string TickerId,
    [property: JsonPropertyName("rows")] List<PriceRowDto> Rows
);

public record TechnicalDataDto(
    [property: JsonPropertyName("sma20")] float Sma20,
    [property: JsonPropertyName("sma50")] float Sma50,
    [property: JsonPropertyName("sma200")] float Sma200
);

public record DailyTickerDto(
    [property: JsonPropertyName("ticker")] string Ticker,
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("open")] float Open,
    [property: JsonPropertyName("high")] float High,
    [property: JsonPropertyName("low")] float Low,
    [property: JsonPropertyName("close")] float Close,
    [property: JsonPropertyName("volume")] float Volume,
    [property: JsonPropertyName("technicalData")] TechnicalDataDto TechnicalData
);

public record DailyDataResponse(
    [property: JsonPropertyName("data")] List<DailyTickerDto> Data
);

// ── Entity ──

public record EntityInfoDto(
    [property: JsonPropertyName("entity_id")] int EntityId,
    [property: JsonPropertyName("is_player")] int IsPlayer,
    [property: JsonPropertyName("available_cash")] double AvailableCash
);

public record EntityRegistrationResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("entity_db_id")] int EntityDbId,
    [property: JsonPropertyName("already_exists")] bool AlreadyExists
);

public record EntityCreateResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("entity_id")] int EntityId,
    [property: JsonPropertyName("is_player")] int IsPlayer,
    [property: JsonPropertyName("available_cash")] double AvailableCash
);

public record EntityGetResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("entity")] EntityInfoDto Entity
);

public record ResolveEntityResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("external_id")] string ExternalId,
    [property: JsonPropertyName("entity_db_id")] int EntityDbId
);

// ── Trade ──

public record PostTradeResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("filled_price")] double FilledPrice,
    [property: JsonPropertyName("order_type")] string? OrderType
)
{
    [JsonPropertyName("unlocked_nodes")]
    public List<UnlockedNodeDto>? UnlockedNodes { get; init; }
};

public record InternalTradeResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("entity_id")] int EntityId,
    [property: JsonPropertyName("ticker_id")] string TickerId,
    [property: JsonPropertyName("shares")] double Shares,
    [property: JsonPropertyName("price")] double Price,
    [property: JsonPropertyName("cash_before")] double CashBefore,
    [property: JsonPropertyName("cash_after")] double CashAfter
);

public record PortfolioLotDto(
    [property: JsonPropertyName("portfolio_id")] int PortfolioId,
    [property: JsonPropertyName("entity_id")] int EntityId,
    [property: JsonPropertyName("ticker_id")] string TickerId,
    [property: JsonPropertyName("shares_held")] double SharesHeld,
    [property: JsonPropertyName("purchase_date")] string PurchaseDate,
    [property: JsonPropertyName("price")] double Price
);

public record PortfolioTotalDto(
    [property: JsonPropertyName("ticker_id")] string TickerId,
    [property: JsonPropertyName("shares_held")] double SharesHeld
);

public record PortfolioResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("entity")] EntityInfoDto Entity,
    [property: JsonPropertyName("lots")] List<PortfolioLotDto> Lots,
    [property: JsonPropertyName("totals")] List<PortfolioTotalDto> Totals
);

public record TradeHistoryRowDto(
    [property: JsonPropertyName("history_id")] int HistoryId,
    [property: JsonPropertyName("entity_id")] int EntityId,
    [property: JsonPropertyName("ticker_id")] string TickerId,
    [property: JsonPropertyName("price_paid")] double PricePaid,
    [property: JsonPropertyName("shares")] double Shares,
    [property: JsonPropertyName("trade_date")] string TradeDate
);

public record TradeHistoryResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("rows")] List<TradeHistoryRowDto> Rows
);

// ── Game State ──

public record GameDateResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("current_date")] string? CurrentDate,
    [property: JsonPropertyName("game_phase")] string? GamePhase,
    [property: JsonPropertyName("message")] string? Message
);

public record ForcedLiquidationDto(
    [property: JsonPropertyName("ticker_id")] string TickerId,
    [property: JsonPropertyName("shares")] double Shares,
    [property: JsonPropertyName("price")] double Price,
    [property: JsonPropertyName("reason")] string Reason
);

public record AdvanceDayResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("previous_date")] string? PreviousDate,
    [property: JsonPropertyName("current_date")] string CurrentDate,
    [property: JsonPropertyName("game_phase")] string GamePhase,
    [property: JsonPropertyName("game_over")] bool GameOver,
    [property: JsonPropertyName("message")] string? Message
)
{
    [JsonPropertyName("unlocked_nodes")]
    public List<UnlockedNodeDto>? UnlockedNodes { get; init; }

    [JsonPropertyName("forced_liquidations")]
    public List<ForcedLiquidationDto>? ForcedLiquidations { get; init; }
};

public record GamePhaseResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("game_phase")] string? GamePhase,
    [property: JsonPropertyName("message")] string? Message
);

public record AdvancePhaseResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("current_date")] string CurrentDate,
    [property: JsonPropertyName("game_phase")] string GamePhase,
    [property: JsonPropertyName("message")] string? Message
);

public record NewGameResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("current_date")] string CurrentDate,
    [property: JsonPropertyName("game_phase")] string GamePhase
);

// ── Dialogue ──

public record DialogueRowDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("date")] string? Date,
    [property: JsonPropertyName("ticker_id")] string? TickerId,
    [property: JsonPropertyName("npc_type")] string? NpcType,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("priority")] string? Priority,
    [property: JsonPropertyName("phase")] string? Phase,
    [property: JsonPropertyName("line_order")] int LineOrder
);

public record DialogueResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("date")] string? Date,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("dialogue")] List<DialogueRowDto> Dialogue
);

public record DialogueGenerationResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("phase")] string Phase,
    [property: JsonPropertyName("interestingness_score")] double InterestingnessScore,
    [property: JsonPropertyName("dynamic_count")] int DynamicCount,
    [property: JsonPropertyName("static_count")] int StaticCount
);

// ── Newspaper ──

public record NewspaperArticleDto(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("category")] string Category
);

public record NewspaperResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("date_display")] string DateDisplay,
    [property: JsonPropertyName("headline")] string Headline,
    [property: JsonPropertyName("articles")] List<NewspaperArticleDto> Articles,
    [property: JsonPropertyName("market_recap")] string MarketRecap,
    [property: JsonPropertyName("from_cache")] bool FromCache
);

// ── Save State ──

public record SaveStateResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("value")] string Value
);

// ── Knowledge Graph ──

public record NodePositionDto(
    [property: JsonPropertyName("x")] float X,
    [property: JsonPropertyName("y")] float Y
);

public record KnowledgeNodeStateDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("prerequisites")] List<string> Prerequisites,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("position")] NodePositionDto Position,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("unlocked_at")] string? UnlockedAt,
    [property: JsonPropertyName("completed_at")] string? CompletedAt,
    [property: JsonPropertyName("reward_mechanic")] string? RewardMechanic,
    [property: JsonPropertyName("trigger_explanation")] string? TriggerExplanation,
    [property: JsonPropertyName("correct_action")] string? CorrectAction
);

public record KnowledgeGraphResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("nodes")] List<KnowledgeNodeStateDto> Nodes,
    [property: JsonPropertyName("categories")] Dictionary<string, CategoryConfig> Categories
);

public record UnlockedNodeDto(
    [property: JsonPropertyName("node_id")] string NodeId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("category")] string Category
);

public record KnowledgeNodeUpdateResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("node_id")] string NodeId,
    [property: JsonPropertyName("new_status")] string? NewStatus,
    [property: JsonPropertyName("message")] string? Message
)
{
    [JsonPropertyName("newly_unlocked")]
    public List<string>? NewlyUnlocked { get; init; }

    [JsonPropertyName("reward_mechanic")]
    public string? RewardMechanic { get; init; }
};

public record KnowledgeNodeCompleteRequest(
    [property: JsonPropertyName("entity_id")] int EntityId,
    [property: JsonPropertyName("node_id")] string NodeId
);

public record UnlockedMechanicsResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("mechanics")] List<string> Mechanics
);

// ── Pending Orders ──

public record PendingOrderDto(
    [property: JsonPropertyName("order_id")] int OrderId,
    [property: JsonPropertyName("ticker_id")] string TickerId,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("order_type")] string OrderType,
    [property: JsonPropertyName("limit_price")] double? LimitPrice
);

public record PendingOrdersResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("orders")] List<PendingOrderDto> Orders
);

public record QueueOrderResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("order_id")] int OrderId,
    [property: JsonPropertyName("message")] string? Message
);

// ── Market Phase Transitions (ACID) ──

public record TradeResultDto(
    [property: JsonPropertyName("ticker")] string Ticker,
    [property: JsonPropertyName("side")] string Side,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("fill_price")] double FillPrice,
    [property: JsonPropertyName("close_price")] double? ClosePrice,
    [property: JsonPropertyName("pnl")] double? Pnl,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string? Message
);

public record OpenMarketsResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("current_date")] string CurrentDate,
    [property: JsonPropertyName("game_phase")] string GamePhase,
    [property: JsonPropertyName("trade_results")] List<TradeResultDto> TradeResults,
    [property: JsonPropertyName("unlocked_nodes")] List<UnlockedNodeDto>? UnlockedNodes,
    [property: JsonPropertyName("message")] string? Message
);

public record CloseMarketsResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("current_date")] string CurrentDate,
    [property: JsonPropertyName("game_phase")] string GamePhase,
    [property: JsonPropertyName("trade_results")] List<TradeResultDto> TradeResults,
    [property: JsonPropertyName("day_pnl")] double DayPnl,
    [property: JsonPropertyName("unlocked_nodes")] List<UnlockedNodeDto>? UnlockedNodes,
    [property: JsonPropertyName("message")] string? Message
);

// ── Arcs (Seasons) ──

public record ArcDefinitionDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("start_date")] string StartDate,
    [property: JsonPropertyName("end_date")] string EndDate,
    [property: JsonPropertyName("newspaper_tone")] string NewspaperTone
);

public record ArcStatusResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("current_date")] string CurrentDate,
    [property: JsonPropertyName("arc")] ArcDefinitionDto? Arc,
    [property: JsonPropertyName("trading_days_remaining")] int? TradingDaysRemaining,
    [property: JsonPropertyName("current_return_pct")] double? CurrentReturnPct,
    [property: JsonPropertyName("projected_grade")] string? ProjectedGrade,
    [property: JsonPropertyName("message")] string? Message
);

public record ArcGradeDto(
    [property: JsonPropertyName("arc_id")] string ArcId,
    [property: JsonPropertyName("arc_name")] string ArcName,
    [property: JsonPropertyName("start_date")] string StartDate,
    [property: JsonPropertyName("end_date")] string EndDate,
    [property: JsonPropertyName("start_value")] double StartValue,
    [property: JsonPropertyName("end_value")] double EndValue,
    [property: JsonPropertyName("return_pct")] double ReturnPct,
    [property: JsonPropertyName("grade")] string Grade,
    [property: JsonPropertyName("cash_multiplier")] double CashMultiplier
);

public record ArcGradesResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("grades")] List<ArcGradeDto> Grades
);

public record ArcTransitionDto(
    [property: JsonPropertyName("completed_arc")] ArcGradeDto CompletedArc,
    [property: JsonPropertyName("next_arc")] ArcDefinitionDto? NextArc,
    [property: JsonPropertyName("cash_before")] double CashBefore,
    [property: JsonPropertyName("cash_after")] double CashAfter
);

public record ArcAdvanceResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("transition")] ArcTransitionDto? Transition,
    [property: JsonPropertyName("message")] string? Message
);

// ── Portfolio History ──

public record NetWorthPointDto(
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("cash")] double Cash,
    [property: JsonPropertyName("holdings_value")] double HoldingsValue,
    [property: JsonPropertyName("net_worth")] double NetWorth
);

public record PortfolioHistoryResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("history")] List<NetWorthPointDto> History
);

// ── Market Movers (Daily Summary) ──

public record MarketMoverDto(
    [property: JsonPropertyName("ticker")] string Ticker,
    [property: JsonPropertyName("close")] double Close,
    [property: JsonPropertyName("change_pct")] double ChangePct
);

public record MarketMoversResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("gainers")] List<MarketMoverDto> Gainers,
    [property: JsonPropertyName("losers")] List<MarketMoverDto> Losers,
    [property: JsonPropertyName("delisted")] List<string> Delisted
);

// ── Shared error envelope ──

public record ErrorResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message
);
