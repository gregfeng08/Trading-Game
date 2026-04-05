using UnityEngine;
using Game.API.DTO;
using System.Threading.Tasks;

namespace Game.API
{
    /// <summary>
    /// API for all database-related functions
    /// </summary>
    public static class DbAPI
    {
        //Post request to tell the Flask server to initialize the DB via the schema
        public static Task<InitDBResponse> InitializeDB()
            => APIClient.PostAsync<object, InitDBResponse>("init_db", new { });

        //Post request to begin tell the database to load the ticker data into the DB
        public static Task<LoadTickerDataResponse> LoadTickers()
            => APIClient.PostAsync<object, LoadTickerDataResponse>("load_tickers", new { });

        //Function to register entities within the DB
        public static Task<EntityRegistrationResponse> RegisterEntity(EntityDTO entity)
            => APIClient.PostAsync<EntityDTO, EntityRegistrationResponse>("register_entity", entity);

        //Function to get the daily data along with the SMA as a parameter for all tickers
        public static Task<DailyDataRetrievalResponse> GetDailyData(string ticker = null, int? limit = null)
        {
            var path = "get_daily_data";
            var sep = '?';
            if (ticker != null) { path += $"{sep}ticker={ticker}"; sep = '&'; }
            if (limit.HasValue) { path += $"{sep}limit={limit.Value}"; }
            return APIClient.GetAsync<DailyDataRetrievalResponse>(path);
        }
    }
}

