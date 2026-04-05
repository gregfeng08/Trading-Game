namespace Game.API.DTO
{
    [System.Serializable]
    public class TickerListResponse
    {
        public string status;
        public TickerDTO[] tickers;
    }

    [System.Serializable]
    public class TickerDTO
    {
        public string ticker_id;
        public string company_name;
        public string description;
    }
}
