namespace Game.API.DTO
{
    [System.Serializable]
    public class GameDateResponse
    {
        public string status;
        public string current_date;
        public string message;
    }

    [System.Serializable]
    public class AdvanceDayResponse
    {
        public string status;
        public string previous_date;
        public string current_date;
        public bool game_over;
        public string message;
    }

    [System.Serializable]
    public class NewGameResponse
    {
        public string status;
        public string message;
        public string current_date;
    }
}
