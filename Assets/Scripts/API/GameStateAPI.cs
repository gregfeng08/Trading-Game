using System.Threading.Tasks;
using Game.API.DTO;

namespace Game.API
{
    public static class GameStateAPI
    {
        public static Task<GameDateResponse> GetGameDate()
            => APIClient.GetAsync<GameDateResponse>("get_game_date");

        public static Task<AdvanceDayResponse> AdvanceDay()
            => APIClient.PostAsync<object, AdvanceDayResponse>("advance_day", new { });

        public static Task<NewGameResponse> NewGame(string startDate = null)
        {
            var path = "new_game";
            if (startDate != null) path += $"?startDate={startDate}";
            return APIClient.PostAsync<object, NewGameResponse>(path, new { });
        }
    }
}
