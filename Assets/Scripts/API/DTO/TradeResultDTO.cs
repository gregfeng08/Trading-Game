namespace Game.API.DTO
{
    [System.Serializable]
    public class TradeResultDTO
    {
        public string status;          // "ok" / "error"
        public string message;         // error details if any

        public string trade_id;        // server-generated id (string is safest)
        public string ticker;

        public string side;
        public int quantity;
        public float fill_price;

        public float cash_after;       // optional if your server returns it
        public float position_after;   // optional (new position size)
    }
}
