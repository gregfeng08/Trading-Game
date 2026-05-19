using TradingGame.Services;

namespace TradingGame.Endpoints;

public static class MarketEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/tickers", (MarketDataService market) =>
            Results.Ok(market.GetTickers()));

        app.MapGet("/prices", (string tickerId, string? startDate, string? endDate, MarketDataService market) =>
            Results.Ok(market.GetPrices(tickerId, startDate, endDate)));

        app.MapGet("/get_daily_data", (string? ticker, int? limit, MarketDataService market) =>
            Results.Ok(market.GetDailyData(ticker, limit)));

        app.MapGet("/market_movers", (string date, MarketDataService market) =>
            Results.Ok(market.GetMarketMovers(date)));
    }
}
