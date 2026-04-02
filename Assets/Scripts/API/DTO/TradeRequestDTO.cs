namespace Game.API.DTO
{
    [System.Serializable]
    public class TradeRequestDTO
    {
        public string entity_id;   // who is trading
        public string ticker;      // e.g. "AAPL"
        public string side;        // "buy" or "sell"
        public int quantity;       // shares/contracts
        public float price;        // optional if you do market orders; keep if your Flask expects it
        public string order_type;  // "market" or "limit" (optional but useful)
    }
}
