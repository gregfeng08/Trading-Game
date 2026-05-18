namespace Game.API.DTO
{
    [System.Serializable]
    public class DailyDataRetrievalResponse
    {
        public DailyTickerDTO[] data;
    }

    [System.Serializable]
    public class DailyTickerDTO
    {
        public string ticker;
        public string date;
        public double open;
        public double high;
        public double low;
        public double close;
        public double volume;
        public TechnicalData technicalData;
    }

    [System.Serializable]
    public class TechnicalData
    {
        public double sma20;
        public double sma50;
        public double sma200;
    }
}