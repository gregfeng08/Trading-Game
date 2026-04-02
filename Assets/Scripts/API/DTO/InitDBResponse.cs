namespace Game.API.DTO
{
    [System.Serializable]
    public class InitDBResponse
    {
        /// <summary>
        /// "ok" or "error"
        /// </summary>
        public string status;

        /// <summary>
        /// Human-readable message (what actually happened)
        /// </summary>
        public string message;

        /// <summary>
        /// True if tables were created on this call
        /// False if DB was already initialized
        /// </summary>
        public bool initialized;
    }
}
