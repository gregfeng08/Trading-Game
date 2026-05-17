namespace Game.API.DTO
{
    [System.Serializable]
    public class GameDateResponse
    {
        public string status;
        public string current_date;
        public string game_phase;
        public string message;
    }

    [System.Serializable]
    public class AdvanceDayResponse
    {
        public string status;
        public string previous_date;
        public string current_date;
        public string game_phase;
        public bool game_over;
        public string message;
    }

    [System.Serializable]
    public class NewGameResponse
    {
        public string status;
        public string message;
        public string current_date;
        public string game_phase;
    }

    [System.Serializable]
    public class GamePhaseResponse
    {
        public string status;
        public string game_phase;
        public string message;
    }

    [System.Serializable]
    public class AdvancePhaseResponse
    {
        public string status;
        public string current_date;
        public string game_phase;
        public string message;
    }
}
