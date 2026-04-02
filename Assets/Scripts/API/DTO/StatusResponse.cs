namespace Game.API.DTO
{
    [System.Serializable]
    public class StatusResponse
    {
        public string status;
        public double server_time;      // was float
        public double uptime_seconds;   // was float
        public bool db_connected;
        public string version;
    }
}
