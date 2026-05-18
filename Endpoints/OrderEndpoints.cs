using TradingGame.Models;
using TradingGame.Services;

namespace TradingGame.Endpoints;

public static class OrderEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/queue_order", (QueueOrderRequest req, OrderService orders) =>
            orders.QueueOrder(req));

        app.MapGet("/pending_orders", (string entityId, EntityService entities, OrderService orders) =>
        {
            var dbId = entities.ResolveExternalId(entityId);
            if (dbId is null)
                return Results.Json(new ErrorResponse("error", "Entity not found"), statusCode: 404);
            return Results.Ok(orders.GetPendingOrders(dbId.Value));
        });

        app.MapDelete("/pending_orders", (string entityId, OrderService orders) =>
            orders.ClearPendingOrders(entityId));

        app.MapDelete("/pending_orders/{orderId}", (int orderId, OrderService orders) =>
            orders.RemoveOrder(orderId));

        app.MapPost("/open_markets", (OpenMarketsRequest req, OrderService orders) =>
            orders.OpenMarkets(req));

        app.MapPost("/close_markets", (CloseMarketsRequest req, OrderService orders) =>
            orders.CloseMarkets(req));

        app.MapGet("/portfolio_history", (int entityId, OrderService orders) =>
            Results.Ok(orders.GetPortfolioHistory(entityId)));
    }
}
