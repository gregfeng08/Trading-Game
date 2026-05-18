namespace Game.API.DTO
{
    [System.Serializable]
    public class PostTradeResponse
    {
        public string status;
        public string message;
        public double filled_price;
        public string order_type;
    }
}