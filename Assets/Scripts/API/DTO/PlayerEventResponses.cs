namespace Game.API.DTO
{
    [System.Serializable]
    public class PlayerEventRequestDTO
    {
        public int entity_id;
        public string event_type;
        public string metadata_json;
    }

    [System.Serializable]
    public class PlayerEventResponse
    {
        public string status;
        public string event_type;
        public string date;
        public UnlockedNodeDTO[] unlocked_nodes;
    }

    [System.Serializable]
    public class NpcQuestCompleteRequestDTO
    {
        public int entity_id;
        public string npc_type;
    }

    [System.Serializable]
    public class NpcQuestCompleteResponse
    {
        public string status;
        public string npc_type;
        public string[] unlocked_nodes;
        public string message;
    }

    [System.Serializable]
    public class NpcQuestDTO
    {
        public string npc_type;
        public string name;
        public string description;
        public string[] unlocks_nodes;
        public string quest_dialogue_hint;
    }

    [System.Serializable]
    public class NpcQuestsResponse
    {
        public string status;
        public NpcQuestDTO[] quests;
    }

    [System.Serializable]
    public class MoverSummaryDTO
    {
        public string ticker;
        public double change_pct;
    }

    [System.Serializable]
    public class DaySkipSummaryDTO
    {
        public string date;
        public double net_worth;
        public MoverSummaryDTO[] top_movers;
    }

    [System.Serializable]
    public class WeekReviewResponse
    {
        public string status;
        public string start_date;
        public string end_date;
        public int days_advanced;
        public DaySkipSummaryDTO[] daily_summaries;
        public UnlockedNodeDTO[] unlocked_nodes;
    }
}
