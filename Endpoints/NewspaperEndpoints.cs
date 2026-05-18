using TradingGame.Models;
using TradingGame.Services;

namespace TradingGame.Endpoints;

public static class NewspaperEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/newspaper", async (string? date, NewspaperService? newspaper) =>
        {
            if (newspaper is null)
                return Results.Json(new ErrorResponse("error", "Newspaper service unavailable (historical_events.json not found)"), statusCode: 503);

            try
            {
                return Results.Ok(await newspaper.GetNewspaper(date));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new ErrorResponse("error", ex.Message), statusCode: 400);
            }
        });

        app.MapGet("/newspaper/image", async (string? date, NewspaperRendererService? renderer) =>
        {
            if (renderer is null)
                return Results.Json(new ErrorResponse("error", "Newspaper renderer unavailable"), statusCode: 503);

            try
            {
                var png = await renderer.GetNewspaperImage(date);
                return Results.File(png, "image/png", $"tribune_{date ?? "today"}.png");
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new ErrorResponse("error", ex.Message), statusCode: 400);
            }
        });
    }
}
