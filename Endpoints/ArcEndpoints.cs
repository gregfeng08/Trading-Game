using TradingGame.Models;
using TradingGame.Services;

namespace TradingGame.Endpoints;

public static class ArcEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/arc/status", (string entityId, ArcService? arcs) =>
        {
            if (arcs is null)
                return Results.Json(new ErrorResponse("error", "Arc system unavailable (arcs.json not found)"), statusCode: 503);
            return Results.Ok(arcs.GetStatus(entityId));
        });

        app.MapPost("/arc/advance", (string entityId, ArcService? arcs) =>
        {
            if (arcs is null)
                return Results.Json(new ErrorResponse("error", "Arc system unavailable (arcs.json not found)"), statusCode: 503);
            try
            {
                return Results.Ok(arcs.CheckAdvance(entityId));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new ErrorResponse("error", ex.Message), statusCode: 400);
            }
        });

        app.MapGet("/arc/grades", (string entityId, EntityService entities, ArcService? arcs) =>
        {
            if (arcs is null)
                return Results.Json(new ErrorResponse("error", "Arc system unavailable (arcs.json not found)"), statusCode: 503);

            var dbId = entities.ResolveExternalId(entityId);
            if (dbId is null)
                return Results.Json(new ErrorResponse("error", "Entity not found"), statusCode: 404);

            return Results.Ok(arcs.GetGrades(dbId.Value));
        });

        app.MapGet("/arc/all", (ArcService? arcs) =>
        {
            if (arcs is null)
                return Results.Json(new ErrorResponse("error", "Arc system unavailable (arcs.json not found)"), statusCode: 503);
            return Results.Ok(new { status = "ok", arcs = arcs.GetAllArcs() });
        });
    }
}
