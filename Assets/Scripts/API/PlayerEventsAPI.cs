using System.Threading.Tasks;
using Game.API.DTO;

namespace Game.API
{
    public static class PlayerEventsAPI
    {
        public static Task<PlayerEventResponse> RecordEvent(PlayerEventRequestDTO req)
            => APIClient.PostAsync<PlayerEventRequestDTO, PlayerEventResponse>("player_events", req);

        public static Task<NpcQuestCompleteResponse> CompleteQuest(NpcQuestCompleteRequestDTO req)
            => APIClient.PostAsync<NpcQuestCompleteRequestDTO, NpcQuestCompleteResponse>("npc_quest/complete", req);

        public static Task<NpcQuestsResponse> GetQuests()
            => APIClient.GetAsync<NpcQuestsResponse>("npc_quests");
    }
}
