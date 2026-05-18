namespace Game.API.DTO
{
    [System.Serializable]
    public class LoadTickersRequestDTO
    {
        public string start_date;
        public string end_date;
        public int top_n = 50;
    }

    [System.Serializable]
    public class LoadTickerDataResponse
    {
        /// <summary>
        /// "ok" or "error"
        /// </summary>
        public string status;

        /// <summary>
        /// Human-readable message (optional)
        /// </summary>
        public string message;

        /// <summary>
        /// Number of tickers loaded or already present
        /// </summary>
        public int ticker_count;
    }
}
