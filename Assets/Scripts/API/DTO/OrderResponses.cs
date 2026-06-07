namespace Game.API.DTO
{
    [System.Serializable]
    public class QueueOrderRequestDTO
    {
        public string entity_id;
        public string ticker;
        public string side;
        public int quantity;
        public string order_type;
        public double limit_price;
        public double stop_price;
    }

    [System.Serializable]
    public class QueueOrderResponse
    {
        public string status;
        public int order_id;
        public string message;
    }

    [System.Serializable]
    public class PendingOrderDTO
    {
        public int order_id;
        public string ticker_id;
        public string side;
        public int quantity;
        public string order_type;
        public double limit_price;
        public double stop_price;
    }

    [System.Serializable]
    public class PendingOrdersResponse
    {
        public string status;
        public PendingOrderDTO[] orders;
    }

    [System.Serializable]
    public class TradeResultDTO
    {
        public string ticker;
        public string side;
        public int quantity;
        public double fill_price;
        public double close_price;
        public double pnl;
        public string status;
        public string message;
    }

    [System.Serializable]
    public class InlineOrderDTO
    {
        public string ticker;
        public string side;
        public int quantity;
        public string order_type;
        public double limit_price;
        public double stop_price;
    }

    [System.Serializable]
    public class OpenMarketsRequestDTO
    {
        public string entity_id;
        public InlineOrderDTO[] orders;
    }

    [System.Serializable]
    public class OpenMarketsResponse
    {
        public string status;
        public string current_date;
        public string game_phase;
        public TradeResultDTO[] trade_results;
        public UnlockedNodeDTO[] unlocked_nodes;
        public string message;
    }

    [System.Serializable]
    public class CloseMarketsRequestDTO
    {
        public string entity_id;
    }

    [System.Serializable]
    public class CloseMarketsResponse
    {
        public string status;
        public string current_date;
        public string game_phase;
        public TradeResultDTO[] trade_results;
        public double day_pnl;
        public UnlockedNodeDTO[] unlocked_nodes;
        public string message;
    }

    [System.Serializable]
    public class NetWorthPointDTO
    {
        public string date;
        public double cash;
        public double holdings_value;
        public double net_worth;
    }

    [System.Serializable]
    public class PortfolioHistoryResponse
    {
        public string status;
        public NetWorthPointDTO[] history;
    }
}
