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
            catch (Exception)
            {
                return Results.Json(new ErrorResponse("error", "An internal error occurred while starting new game."), statusCode: 500);
            }
        });

        app.MapPost("/advance_week", (string entityId, int? days, GameStateService game, EntityService entities) =>
        {
            try
            {
                var entityDbId = entities.ResolveExternalId(entityId);
                if (entityDbId is null)
                    return Results.Json(new ErrorResponse("error", $"Entity '{entityId}' not found"), statusCode: 404);
                return Results.Ok(game.AdvanceWeek(entityDbId.Value, days ?? 5));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new ErrorResponse("error", ex.Message), statusCode: 400);
            }
        });

        app.MapGet("/dialogue", (string? date, string? npcType, string? tickerId, string? category, GameStateService game) =>
            Results.Ok(game.GetDialogue(date, npcType, tickerId, category)));

        app.MapPost("/dialogue/generate", async (string? entityId, NpcDialogueService? dialogueService,
            GameStateService game, EntityService entities) =>
        {
            if (dialogueService is null)
                return Results.Json(new ErrorResponse("error", "Dialogue service not available"), statusCode: 503);

            try
            {
                var dateResp = game.GetGameDate();
                if (dateResp.CurrentDate is null)
                    return Results.Json(new ErrorResponse("error", "No active game"), statusCode: 400);

                int? entityDbId = null;
                if (entityId is not null)
                    entityDbId = entities.ResolveExternalId(entityId);

                var phase = dateResp.GamePhase ?? "pre_market";
                var result = await dialogueService.GenerateForPhase(dateResp.CurrentDate, phase, entityDbId);
                return Results.Ok(result);
            }
            catch (Exception)
            {
                return Results.Json(new ErrorResponse("error", "An internal error occurred while generating dialogue."), statusCode: 500);
            }
        });
    }
}
