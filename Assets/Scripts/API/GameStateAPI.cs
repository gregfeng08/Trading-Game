using System.Threading.Tasks;
using Game.API.DTO;

namespace Game.API
{
    public static class GameStateAPI
    {
        public static Task<GameDateResponse> GetGameDate()
            => APIClient.GetAsync<GameDateResponse>("get_game_date");

        public static Task<GamePhaseResponse> GetGamePhase()
            => APIClient.GetAsync<GamePhaseResponse>("get_game_phase");

        public static Task<AdvancePhaseResponse> AdvancePhase()
            => APIClient.PostAsync<object, AdvancePhaseResponse>("advance_phase", new { });

        public static Task<AdvanceDayResponse> AdvanceDay()
        {
            var path = "advance_day";
            if (!string.IsNullOrEmpty(APIBootstrapper.EntityExternalId))
                path += $"?entityId={APIBootstrapper.EntityExternalId}";
            return APIClient.PostAsync<object, AdvanceDayResponse>(path, new { });
        }

        public static Task<NewGameResponse> NewGame(string startDate = null)
        {
            var path = "new_game";
            if (startDate != null) path += $"?startDate={startDate}";
            return APIClient.PostAsync<object, NewGameResponse>(path, new { });
        }
    }
}
