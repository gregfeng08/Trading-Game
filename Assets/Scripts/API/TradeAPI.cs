using UnityEngine;
using System.Threading.Tasks;
using Game.API.DTO;

namespace Game.API
{
    /// <summary>
    /// API for trade-related functions
    /// </summary>
    public static class TradeAPI
    {
        public static Task<PostTradeResponse> PostTrade(TradeRequestDTO req)
            => APIClient.PostAsync<TradeRequestDTO, PostTradeResponse>("post_trade", req);

        public static Task<ResolveEntityResponse> ResolveEntity(string externalId)
            => APIClient.GetAsync<ResolveEntityResponse>($"resolve_entity?externalId={externalId}");

        public static Task<PortfolioResponse> GetPortfolio(int entityDbId)
            => APIClient.GetAsync<PortfolioResponse>($"portfolio?entityId={entityDbId}");

        public static Task<TradeHistoryResponse> GetTradeHistory(int entityDbId, string tickerId = null)
        {
            var path = $"trade_history?entityId={entityDbId}";
            if (tickerId != null) path += $"&tickerId={tickerId}";
            return APIClient.GetAsync<TradeHistoryResponse>(path);
        }
    }
}
