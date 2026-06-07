using System.Text.Json.Serialization;

namespace TradingGame.Models;

public class LoadTickersRequest
{
    [JsonPropertyName("start_date")]
    public string StartDate { get; set; } = "2005-01-01";

    [JsonPropertyName("end_date")]
    public string EndDate { get; set; } = "2010-12-31";

    [JsonPropertyName("top_n")]
    public int TopN { get; set; } = 50;
}

public class RegisterEntityRequest
{
    [JsonPropertyName("entity_id")]
    public required string EntityId { get; set; }

    [JsonPropertyName("entity_type")]
    public required string EntityType { get; set; }

    [JsonPropertyName("display_name")]
    public required string DisplayName { get; set; }

    [JsonPropertyName("starting_cash")]
    public double StartingCash { get; set; } = 10_000.0;
}

public class CreateEntityRequest
{
    [JsonPropertyName("isPlayer")]
    public int IsPlayer { get; set; }

    [JsonPropertyName("starting_cash")]
    public double StartingCash { get; set; }
}

/// <summary>
/// Matches Unity's TradeRequestDTO: { entity_id, ticker, side, quantity, price, order_type }
/// </summary>
public class PostTradeRequest
{
    [JsonPropertyName("entity_id")]
    public required string EntityId { get; set; }

    [JsonPropertyName("ticker")]
    public required string Ticker { get; set; }

    [JsonPropertyName("side")]
    public required string Side { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("price")]
    public double Price { get; set; }

    [JsonPropertyName("order_type")]
    public string OrderType { get; set; } = "market";
}

/// <summary>
/// Internal trade request (for direct API usage, not called by Unity).
/// </summary>
public class TradeRequest
{
    [JsonPropertyName("entity_id")]
    public int EntityId { get; set; }

    [JsonPropertyName("ticker_id")]
    public required string TickerId { get; set; }

    [JsonPropertyName("shares")]
    public double Shares { get; set; }

    [JsonPropertyName("date")]
    public required string Date { get; set; }
}

public class QueueOrderRequest
{
    [JsonPropertyName("entity_id")]
    public required string EntityId { get; set; }

    [JsonPropertyName("ticker")]
    public required string Ticker { get; set; }

    [JsonPropertyName("side")]
    public required string Side { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("order_type")]
    public string OrderType { get; set; } = "market";

    [JsonPropertyName("limit_price")]
    public double? LimitPrice { get; set; }

    [JsonPropertyName("stop_price")]
    public double? StopPrice { get; set; }
}

public class InlineOrder
{
    [JsonPropertyName("ticker")]
    public required string Ticker { get; set; }

    [JsonPropertyName("side")]
    public required string Side { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("order_type")]
    public string OrderType { get; set; } = "market";

    [JsonPropertyName("limit_price")]
    public double? LimitPrice { get; set; }

    [JsonPropertyName("stop_price")]
    public double? StopPrice { get; set; }
}

public class OpenMarketsRequest
{
    [JsonPropertyName("entity_id")]
    public required string EntityId { get; set; }

    [JsonPropertyName("orders")]
    public List<InlineOrder>? Orders { get; set; }
}

public class CloseMarketsRequest
{
    [JsonPropertyName("entity_id")]
    public required string EntityId { get; set; }
}

public class SaveStateRequest
{
    [JsonPropertyName("key")]
    public required string Key { get; set; }

    [JsonPropertyName("value")]
    public required string Value { get; set; }
}
