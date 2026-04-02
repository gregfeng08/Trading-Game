using UnityEngine;

namespace Game.API.DTO
{
    /// <summary>
    /// Data-transfer object for sending daily price data
    /// </summary>
    public class DailyDataRetrievalResponse
    {
        public DailyTickerDTO[] data;
    }

    /// <summary>
    /// Daily Ticker DTO object on a per-ticker basis
    /// </summary>
    [System.Serializable]
    public class DailyTickerDTO
    {
        public string ticker;
        public string date;
        public float open;
        public float high;
        public float low;
        public float close;
        public float volume;

        public TechnicalData technicalData;
    }

    /// <summary>
    /// Daily Technical Data
    /// </summary>
    [System.Serializable]
    public class TechnicalData
    {
        public float sma20;
        public float sma50;
        public float sma200;
    }
}