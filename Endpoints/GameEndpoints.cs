using TradingGame.Models;
using TradingGame.Services;

namespace TradingGame.Endpoints;

public static class GameEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/get_game_date", (GameStateService game) =>
            Results.Ok(game.GetGameDate()));

        app.MapPost("/advance_day", (string? entityId, GameStateService game, EntityService entities) =>
        {
            try
            {
                int? entityDbId = null;
                if (entityId is not null)
                    entityDbId = entities.ResolveExternalId(entityId);
                return Results.Ok(game.AdvanceDay(entityDbId));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new ErrorResponse("error", ex.Message), statusCode: 400);
            }
        });

        app.MapGet("/get_game_phase", (GameStateService game) =>
            Results.Ok(game.GetGamePhase()));

        app.MapPost("/advance_phase", (GameStateService game) =>
        {
            try
            {
                return Results.Ok(game.AdvancePhase());
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new ErrorResponse("error", ex.Message), statusCode: 400);
            }
        });

        app.MapPost("/new_game", (string? startDate, GameStateService game) =>
        {
            try
            {
                return Results.Ok(game.NewGame(startDate));
            }
            catch (Exception ex)
            {
                return Results.Json(new ErrorResponse("error", ex.Message), statusCode: 500);
            }
        });

        app.MapGet("/dialogue", (string? date, string? npcType, string? tickerId, string? category, GameStateService game) =>
            Results.Ok(game.GetDialogue(date, npcType, tickerId, category)));
    }
}
