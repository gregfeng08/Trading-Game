namespace Game.API.DTO
{
    [System.Serializable]
    public class PricesResponse
    {
        public string status;
        public string ticker_id;
        public PriceRowDTO[] rows;
    }

    [System.Serializable]
    public class PriceRowDTO
    {
        public string ticker_id;
        public string date;
        public float open_price;
        public float high_price;
        public float low_price;
        public float close_price;
    }
}
