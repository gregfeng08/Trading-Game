using TradingGame.Models;
using TradingGame.Services;

namespace TradingGame.Endpoints;

public static class KnowledgeGraphEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/knowledge_graph", (int entityId, KnowledgeGraphService kg) =>
            Results.Ok(kg.GetGraph(entityId)));

        app.MapPost("/knowledge_graph/complete", (KnowledgeNodeCompleteRequest req, KnowledgeGraphService kg, GameStateService game) =>
        {
            var dateResp = game.GetGameDate();
            var gameDate = dateResp.CurrentDate ?? "1970-01-01";

            var result = kg.CompleteNode(req.EntityId, req.NodeId, gameDate);
            if (result.Status == "error")
                return Results.Json(result, statusCode: 400);
            return Results.Ok(result);
        });

        app.MapPost("/knowledge_graph/init", (int entityId, KnowledgeGraphService kg, GameStateService game) =>
        {
            var dateResp = game.GetGameDate();
            var gameDate = dateResp.CurrentDate ?? "1970-01-01";

            var unlocked = kg.InitializeForEntity(entityId, gameDate);
            return Results.Ok(new { status = "ok", unlocked_nodes = unlocked });
        });

        app.MapGet("/knowledge_graph/check_triggers", (int entityId, KnowledgeGraphService kg, GameStateService game) =>
        {
            var dateResp = game.GetGameDate();
            var gameDate = dateResp.CurrentDate ?? "1970-01-01";

            var unlocked = kg.EvaluateTriggers(entityId, gameDate);
            return Results.Ok(new { status = "ok", newly_unlocked = unlocked });
        });

        app.MapGet("/knowledge_graph/unlocks", (int entityId, KnowledgeGraphService kg) =>
            Results.Ok(new UnlockedMechanicsResponse("ok", kg.GetUnlockedMechanics(entityId))));

        app.MapGet("/knowledge_graph/config", (KnowledgeGraphService kg) =>
            Results.Ok(kg.Config));

        app.MapGet("/knowledge_graph/node_content", async (int entityId, string nodeId,
            DynamicNodeContentService? dynContent, KnowledgeGraphService kg, GameStateService game) =>
        {
            var dateResp = game.GetGameDate();
            var gameDate = dateResp.CurrentDate ?? "1970-01-01";

            if (dynContent is not null)
            {
                var content = await dynContent.GetContentWithFallback(entityId, nodeId, gameDate);
                return Results.Ok(new { status = "ok", node_id = nodeId, content, is_personalized = dynContent.GetCachedContent(entityId, nodeId) is not null });
            }

            var node = kg.Config.Nodes.FirstOrDefault(n => n.Id == nodeId);
            return Results.Ok(new { status = "ok", node_id = nodeId, content = node?.Content ?? "", is_personalized = false });
        });
    }
}
