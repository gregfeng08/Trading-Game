namespace Game.API.DTO
{
    [System.Serializable]
    public class PingResponse
    {
        public string status;
        public double server_time; // was float
    }
}
