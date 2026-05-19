namespace Game.API.DTO
{
    [System.Serializable]
    public class MarketMoverDTO
    {
        public string ticker;
        public double close;
        public double change_pct;
    }

    [System.Serializable]
    public class MarketMoversResponse
    {
        public string status;
        public string date;
        public MarketMoverDTO[] gainers;
        public MarketMoverDTO[] losers;
        public string[] delisted;
    }
}
