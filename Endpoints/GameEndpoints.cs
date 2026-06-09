using TradingGame.Data;
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

        app.MapPost("/tutorial/cleanup", (string entityId, Database db, EntityService entities, PlayerContextService? playerContext) =>
        {
            try
            {
                var entityDbId = entities.ResolveExternalId(entityId);
                if (entityDbId is null)
                    return Results.Json(new ErrorResponse("error", $"Entity '{entityId}' not found"), statusCode: 404);

                using var conn = db.Open();
                using var tx = conn.BeginTransaction();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM portfolio WHERE entity_id = @eid;";
                    cmd.Parameters.AddWithValue("@eid", entityDbId.Value);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM trade_history WHERE entity_id = @eid;";
                    cmd.Parameters.AddWithValue("@eid", entityDbId.Value);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM pending_orders WHERE entity_id = @eid;";
                    cmd.Parameters.AddWithValue("@eid", entityDbId.Value);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM net_worth_history WHERE entity_id = @eid;";
                    cmd.Parameters.AddWithValue("@eid", entityDbId.Value);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "UPDATE entity SET available_cash = 10000.0 WHERE entity_id = @eid;";
                    cmd.Parameters.AddWithValue("@eid", entityDbId.Value);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                playerContext?.InvalidateCache();

                return Results.Ok(new { status = "ok", message = "Tutorial trades cleared, cash reset to $10,000" });
            }
            catch (Exception)
            {
                return Results.Json(new ErrorResponse("error", "Failed to clean up tutorial"), statusCode: 500);
            }
        });

        app.MapGet("/casey_comment", async (string entityId, CaseyCommentService? casey,
            GameStateService game, EntityService entities) =>
        {
            if (casey is null)
                return Results.Json(new ErrorResponse("error", "Casey comment service not available"), statusCode: 503);

            try
            {
                var dateResp = game.GetGameDate();
                if (dateResp.CurrentDate is null)
                    return Results.Json(new ErrorResponse("error", "No active game"), statusCode: 400);

                var entityDbId = entities.ResolveExternalId(entityId);
                if (entityDbId is null)
                    return Results.Json(new ErrorResponse("error", $"Entity '{entityId}' not found"), statusCode: 404);

                var comment = await casey.GenerateComment(entityDbId.Value, dateResp.CurrentDate);
                return Results.Ok(new { comment = comment ?? "" });
            }
            catch (Exception)
            {
                return Results.Ok(new { comment = "" });
            }
        });

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
