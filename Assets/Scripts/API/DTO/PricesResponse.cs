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
        public double open_price;
        public double high_price;
        public double low_price;
        public double close_price;
    }
}
