using UnityEngine;
using System.Threading.Tasks;
using Game.API.DTO;

namespace Game.API
{
    /// <summary>
    /// API for trade-related function
    /// </summary>
    public static class TradeAPI
    {
        public static Task<PostTradeResponse> PostTrade(TradeRequestDTO req)
            => APIClient.PostAsync<TradeRequestDTO, PostTradeResponse>("post_trade", req);
    }
}
