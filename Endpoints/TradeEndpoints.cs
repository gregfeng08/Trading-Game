using TradingGame.Models;
using TradingGame.Services;

namespace TradingGame.Endpoints;

public static class TradeEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/post_trade", (PostTradeRequest req, TradingService trading) =>
            trading.PostTrade(req));

        app.MapPost("/trade", (TradeRequest req, TradingService trading) =>
            trading.Trade(req));

        app.MapGet("/portfolio", (int entityId, TradingService trading) =>
        {
            var result = trading.GetPortfolio(entityId);
            if (result is null)
                return Results.Json(new ErrorResponse("error", "Entity not found"), statusCode: 404);
            return Results.Ok(result);
        });

        app.MapGet("/trade_history", (int? entityId, string? tickerId, TradingService trading) =>
            Results.Ok(trading.GetTradeHistory(entityId, tickerId)));
    }
}
