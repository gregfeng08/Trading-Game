namespace Game.API.DTO
{
    [System.Serializable]
    public class TradeRequestDTO
    {
        public string entity_id;
        public string ticker;
        public string side;
        public int quantity;
        public double price;
        public string order_type;
    }
}
