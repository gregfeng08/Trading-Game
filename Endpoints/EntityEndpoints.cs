using TradingGame.Models;
using TradingGame.Services;

namespace TradingGame.Endpoints;

public static class EntityEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/register_entity", (RegisterEntityRequest req, EntityService entities) =>
        {
            try
            {
                return Results.Ok(entities.Register(req));
            }
            catch (Exception ex)
            {
                return Results.Json(new ErrorResponse("error", ex.Message), statusCode: 500);
            }
        });

        app.MapPost("/entity_create", (CreateEntityRequest req, EntityService entities) =>
            Results.Ok(entities.Create(req)));

        app.MapGet("/entity", (int entityId, EntityService entities) =>
        {
            var entity = entities.GetEntity(entityId);
            if (entity is null)
                return Results.Json(new ErrorResponse("error", "Entity not found"), statusCode: 404);
            return Results.Ok(new EntityGetResponse("ok", entity));
        });

        app.MapGet("/resolve_entity", (string externalId, EntityService entities) =>
        {
            var result = entities.Resolve(externalId);
            if (result is null)
                return Results.Json(new ErrorResponse("error", $"External ID '{externalId}' not found"), statusCode: 404);
            return Results.Ok(result);
        });
    }
}
