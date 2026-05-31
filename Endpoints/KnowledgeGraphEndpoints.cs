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

        app.MapGet("/knowledge_graph/node_content", (int entityId, string nodeId,
            DynamicNodeContentService? dynContent, KnowledgeGraphService kg, GameStateService game) =>
        {
            var node = kg.Config.Nodes.FirstOrDefault(n => n.Id == nodeId);
            var staticContent = node?.Content ?? "";

            if (dynContent is null)
                return Results.Ok(new { status = "ok", node_id = nodeId, content = staticContent, is_personalized = false });

            var cached = dynContent.GetCachedContent(entityId, nodeId);
            if (cached is not null)
                return Results.Ok(new { status = "ok", node_id = nodeId, content = cached, is_personalized = true });

            var dateResp = game.GetGameDate();
            var gameDate = dateResp.CurrentDate ?? "1970-01-01";
            _ = Task.Run(async () =>
            {
                try { await dynContent.GeneratePersonalizedContent(entityId, nodeId, gameDate, node?.TriggerExplanation); }
                catch (Exception ex) { Console.WriteLine($"[NodeContent] Background generation failed: {ex.Message}"); }
            });

            return Results.Ok(new { status = "ok", node_id = nodeId, content = staticContent, is_personalized = false });
        });
    }
}
