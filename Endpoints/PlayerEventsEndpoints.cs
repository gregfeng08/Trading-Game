using System.Text.Json;
using TradingGame.Models;
using TradingGame.Services;

namespace TradingGame.Endpoints;

public static class PlayerEventsEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/player_events", (PlayerEventRequest req, KnowledgeGraphService kg, GameStateService game) =>
        {
            var dateResp = game.GetGameDate();
            if (dateResp.CurrentDate is null)
                return Results.Json(new ErrorResponse("error", "No active game"), statusCode: 400);

            if (req.EventType is not "newspaper_read" and not "npc_interaction" and not "tutorial_first_trade")
                return Results.Json(new ErrorResponse("error", "event_type must be 'newspaper_read', 'npc_interaction', or 'tutorial_first_trade'"), statusCode: 400);

            var metadataJson = req.MetadataJson
                ?? (req.Metadata is { Count: > 0 } ? JsonSerializer.Serialize(req.Metadata) : null);

            kg.RecordPlayerEvent(req.EntityId, req.EventType, dateResp.CurrentDate, metadataJson);

            var unlocked = kg.EvaluateTriggers(req.EntityId, dateResp.CurrentDate);

            return Results.Ok(new
            {
                status = "ok",
                event_type = req.EventType,
                date = dateResp.CurrentDate,
                unlocked_nodes = unlocked.Count > 0 ? unlocked : null
            });
        });

        app.MapPost("/npc_quest/complete", (NpcQuestRequest req, KnowledgeGraphService kg, GameStateService game) =>
        {
            var dateResp = game.GetGameDate();
            if (dateResp.CurrentDate is null)
                return Results.Json(new ErrorResponse("error", "No active game"), statusCode: 400);

            var result = kg.CompleteNpcQuest(req.EntityId, req.NpcType, dateResp.CurrentDate);
            if (result.Status == "error")
                return Results.Json(result, statusCode: 400);
            return Results.Ok(result);
        });

        app.MapGet("/npc_quests", (KnowledgeGraphService kg) =>
        {
            var quests = kg.Config.NpcQuests ?? [];
            return Results.Ok(new { status = "ok", quests });
        });
    }
}
