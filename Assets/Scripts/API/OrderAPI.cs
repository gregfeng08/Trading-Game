using System.Threading.Tasks;
using Game.API.DTO;

namespace Game.API
{
    public static class OrderAPI
    {
        public static Task<QueueOrderResponse> QueueOrder(QueueOrderRequestDTO req)
            => APIClient.PostAsync<QueueOrderRequestDTO, QueueOrderResponse>("queue_order", req);

        public static Task<PendingOrdersResponse> GetPendingOrders(string entityId)
            => APIClient.GetAsync<PendingOrdersResponse>($"pending_orders?entityId={entityId}");

        public static Task<OpenMarketsResponse> OpenMarkets(OpenMarketsRequestDTO req)
            => APIClient.PostAsync<OpenMarketsRequestDTO, OpenMarketsResponse>("open_markets", req);

        public static Task<CloseMarketsResponse> CloseMarkets(CloseMarketsRequestDTO req)
            => APIClient.PostAsync<CloseMarketsRequestDTO, CloseMarketsResponse>("close_markets", req);

        public static Task<PortfolioHistoryResponse> GetPortfolioHistory(int entityDbId)
            => APIClient.GetAsync<PortfolioHistoryResponse>($"portfolio_history?entityId={entityDbId}");
    }
}
