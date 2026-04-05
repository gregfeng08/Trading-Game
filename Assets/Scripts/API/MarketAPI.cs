using System.Threading.Tasks;
using Game.API.DTO;

namespace Game.API
{
    public static class MarketAPI
    {
        public static Task<TickerListResponse> GetTickers()
            => APIClient.GetAsync<TickerListResponse>("tickers");

        public static Task<PricesResponse> GetPrices(string tickerId, string startDate = null, string endDate = null)
        {
            var path = $"prices?tickerId={tickerId}";
            if (startDate != null) path += $"&startDate={startDate}";
            if (endDate != null) path += $"&endDate={endDate}";
            return APIClient.GetAsync<PricesResponse>(path);
        }
    }
}
