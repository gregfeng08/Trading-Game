namespace Game.API.DTO
{
    [System.Serializable]
    public class DialogueItemDTO
    {
        public int id;
        public string date;
        public string ticker_id;
        public string npc_type;
        public string category;
        public string text;
        public string source;
        public string priority;
        public string phase;
        public int line_order;
    }

    [System.Serializable]
    public class DialogueResponse
    {
        public string status;
        public string date;
        public int count;
        public DialogueItemDTO[] dialogue;
    }

    [System.Serializable]
    public class DialogueGenerateRequestDTO
    {
        public string entity_id;
    }

    [System.Serializable]
    public class DialogueGenerateResponse
    {
        public string status;
        public string date;
        public string phase;
        public double interestingness_score;
        public int dynamic_count;
        public int static_count;
    }
}
