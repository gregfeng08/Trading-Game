using TradingGame.Models;
using TradingGame.Services;

namespace TradingGame.Endpoints;

public static class SaveStateEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/save_state", (SaveStateRequest req, GameStateService game) =>
            Results.Ok(game.SetState(req.Key, req.Value)));

        app.MapGet("/save_state", (string key, GameStateService game) =>
        {
            var result = game.GetState(key);
            if (result is null)
                return Results.Json(new ErrorResponse("error", "Key not found"), statusCode: 404);
            return Results.Ok(result);
        });
    }
}
