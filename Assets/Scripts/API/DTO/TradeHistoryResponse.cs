namespace Game.API.DTO
{
    [System.Serializable]
    public class TradeHistoryResponse
    {
        public string status;
        public TradeHistoryRowDTO[] rows;
    }

    [System.Serializable]
    public class TradeHistoryRowDTO
    {
        public int history_id;
        public int entity_id;
        public string ticker_id;
        public double price_paid;
        public double shares;
        public string trade_date;
    }
}
