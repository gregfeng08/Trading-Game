using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using TradingGame.Data;
using TradingGame.Models;

namespace TradingGame.Services;

public class NewspaperService
{
    private readonly Database _db;
    private readonly GameStateService _gameState;
    private readonly PlayerContextService _playerContext;
    private readonly InterestingnessService _interestingness;
    private readonly HttpClient _httpClient;
    private readonly List<HistoricalEvent> _events;
    private readonly string? _apiKey;
    private readonly string _model;

    private readonly ArcService? _arcService;

    public NewspaperService(Database db, GameStateService gameState,
        PlayerContextService playerContext, InterestingnessService interestingness,
        string eventsPath, ArcService? arcService = null)
    {
        _db = db;
        _gameState = gameState;
        _playerContext = playerContext;
        _interestingness = interestingness;
        _arcService = arcService;
        _httpClient = new HttpClient();
        _apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        _model = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL") ?? "claude-sonnet-4-20250514";

        var json = File.ReadAllText(eventsPath);
        _events = JsonSerializer.Deserialize<List<HistoricalEvent>>(json) ?? [];
    }

    public string ResolveCurrentDate()
    {
        using var conn = _db.Open();
        return _gameState.GetSaveValue(conn, "current_date")
            ?? throw new InvalidOperationException("No active game. Call /new_game first.");
    }

    public async Task<NewspaperResponse> GetNewspaper(string? date, int? entityId = null)
    {
        using var conn = _db.Open();
        date ??= _gameState.GetSaveValue(conn, "current_date")
            ?? throw new InvalidOperationException("No active game. Call /new_game first.");

        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("ANTHROPIC_API_KEY environment variable is not set.");

        PlayerContext? playerCtx = null;
        if (entityId.HasValue)
            playerCtx = _playerContext.BuildContext(entityId.Value, date, "pre_market");

        var (marketScore, _) = _interestingness.Evaluate(date, "pre_market", null, InterestSignals.MarketOnly);

        var context = BuildContext(conn, date);
        var newspaper = await GenerateNewspaper(date, context, playerCtx, marketScore);

        if (entityId.HasValue)
            newspaper = newspaper with { PlayerSidebar = BuildPlayerSidebar(playerCtx!) };

        return newspaper;
    }

    private PlayerNewsSidebarDto? BuildPlayerSidebar(PlayerContext ctx)
    {
        if (ctx.Portfolio.Holdings.Count == 0)
            return null;

        var mentions = new List<string>();
        var heldTickers = ctx.Portfolio.Holdings.ToDictionary(h => h.TickerId);

        foreach (var gainer in ctx.Market.TopGainers)
        {
            if (heldTickers.TryGetValue(gainer.TickerId, out var holding))
                mentions.Add($"Your holding {gainer.TickerId} rose {gainer.ChangePct:+0.0}% today (you hold {holding.SharesHeld:F0} shares)");
        }
        foreach (var loser in ctx.Market.TopLosers)
        {
            if (heldTickers.TryGetValue(loser.TickerId, out var holding))
                mentions.Add($"Your holding {loser.TickerId} fell {loser.ChangePct:0.0}% today (you hold {holding.SharesHeld:F0} shares)");
        }

        string? impact = null;
        if (ctx.Arc.ProjectedReturnPct.HasValue)
            impact = $"Your portfolio is {(ctx.Arc.ProjectedReturnPct >= 0 ? "up" : "down")} {Math.Abs(ctx.Arc.ProjectedReturnPct.Value):F1}% this arc (projected grade: {ctx.Arc.ProjectedGrade ?? "?"})";

        if (mentions.Count == 0 && impact is null)
            return null;

        return new PlayerNewsSidebarDto(mentions, impact);
    }

    private NewspaperContext BuildContext(SqliteConnection conn, string date)
    {
        var gameDate = DateOnly.ParseExact(date, "yyyy-MM-dd");

        var nearbyEvents = _events
            .Where(e =>
            {
                var eventDate = DateOnly.ParseExact(e.Date, "yyyy-MM-dd");
                var diff = Math.Abs(gameDate.DayNumber - eventDate.DayNumber);
                return diff <= 7;
            })
            .OrderBy(e => e.Date)
            .ToList();

        var topMovers = GetTopMovers(conn, date);
        var prevDate = GetPreviousTradingDate(conn, date);

        return new NewspaperContext(date, gameDate, nearbyEvents, topMovers, prevDate);
    }

    private List<MoverDto> GetTopMovers(SqliteConnection conn, string date)
    {
        var prevDate = GetPreviousTradingDate(conn, date);
        if (prevDate is null) return [];

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT t.ticker_id, t.close_price, p.close_price
            FROM ticker_prices t
            JOIN ticker_prices p ON t.ticker_id = p.ticker_id AND p.date = @prev
            WHERE t.date = @d AND p.close_price > 0
            ORDER BY t.ticker_id;
            """;
        cmd.Parameters.AddWithValue("@d", date);
        cmd.Parameters.AddWithValue("@prev", prevDate);

        var movers = new List<MoverDto>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var ticker = reader.GetString(0);
            var todayClose = reader.GetDouble(1);
            var prevClose = reader.GetDouble(2);
            var pctChange = (todayClose - prevClose) / prevClose * 100.0;
            movers.Add(new MoverDto(ticker, prevClose, todayClose, Math.Round(pctChange, 2)));
        }

        var sorted = movers.OrderByDescending(m => Math.Abs(m.PctChange)).ToList();
        var gainers = sorted.Where(m => m.PctChange > 0).Take(5).ToList();
        var losers = sorted.Where(m => m.PctChange < 0).Take(5).ToList();

        return gainers.Concat(losers).ToList();
    }

    private string? GetPreviousTradingDate(SqliteConnection conn, string date)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT MAX(date) FROM ticker_prices WHERE date < @d;";
        cmd.Parameters.AddWithValue("@d", date);
        var result = cmd.ExecuteScalar();
        return result is not null and not DBNull ? (string)result : null;
    }

    private async Task<NewspaperResponse> GenerateNewspaper(string date, NewspaperContext ctx, PlayerContext? playerCtx = null, double marketInterestingness = 0)
    {
        var dateDisplay = ctx.GameDate.ToString("dddd, MMMM d, yyyy");

        var arcTone = GetArcToneForDate(date);
        var toneInstruction = arcTone switch
        {
            "cautiously_optimistic" => "The overall editorial tone should be cautiously optimistic — recovery is underway but fragile. Headlines lean hopeful with notes of caution.",
            "fearful_volatile" => "The overall editorial tone should be fearful and urgent — markets are volatile, uncertainty is high. Headlines should convey alarm and the sense that anything could happen.",
            "bullish_confident" => "The overall editorial tone should be confident and bullish — the economy is strong, markets are climbing. Headlines are forward-looking and assertive.",
            "uncertain_mixed" => "The overall editorial tone should reflect uncertainty — mixed signals, conflicting data. Headlines ask questions as much as they answer them.",
            _ => ""
        };

        var systemPrompt = """
            You are a newspaper editor for a fictional daily financial newspaper called "The Market Tribune".
            You write in the style of mid-2000s financial journalism — authoritative, slightly formal, with
            a sense of gravity about market events. Your newspaper serves retail investors who want to
            understand what's happening in the markets and the world.

            CRITICAL RULES:
            - ONLY reference facts, events, and data explicitly provided in the user message below.
            - Do NOT invent, estimate, or recall any specific numbers (stock prices, index levels,
              percentages, dollar amounts, economic statistics) that are not in the provided data.
            - If market mover data is provided, the market_recap should ONLY cite tickers, prices,
              and percentages from that data. Do not add index levels (S&P, Dow, NASDAQ) or other
              statistics unless they appear in the provided data.
            - If no market data is provided (first day), write a brief general-tone outlook without
              citing any specific numbers.
            - Do NOT reference future events. Write only from the perspective of someone living on
              this exact date who does not know what happens next.
            - Articles should be grounded in the provided events. You may describe their significance
              and implications, but do not add specific claims or statistics not present in the input.

            """ + (toneInstruction.Length > 0 ? toneInstruction + "\n\n" : "") + """
            You must respond with valid JSON only, no markdown fencing. Use this exact structure:
            {
              "headline": "The main headline of the day (compelling, newspaper-style)",
              "articles": [
                {
                  "title": "Article headline",
                  "body": "2-3 sentence article body. Concise and informative.",
                  "category": "one of: markets, economy, politics, technology, world"
                }
              ],
              "market_recap": "A 2-3 sentence summary of yesterday's market action referencing only the provided data."
            }

            Generate exactly 3 articles. At least one should reference market-relevant news.
            Base all articles on the events and data provided below — do not supplement with outside knowledge.
            """;

        var userPrompt = new StringBuilder();
        userPrompt.AppendLine($"Today's date: {dateDisplay}");
        userPrompt.AppendLine();

        if (ctx.NearbyEvents.Count > 0)
        {
            userPrompt.AppendLine("Recent and upcoming real-world events around this date:");
            foreach (var e in ctx.NearbyEvents)
                userPrompt.AppendLine($"- [{e.Date}] {e.Headline}: {e.Summary}");
            userPrompt.AppendLine();
        }

        if (ctx.TopMovers.Count > 0)
        {
            userPrompt.AppendLine($"Yesterday's market movers (vs previous close on {ctx.PreviousDate}):");
            userPrompt.AppendLine("NOTE: Prices are split-adjusted. In articles, reference only the ticker name and percentage change — do NOT quote the raw dollar prices below.");
            foreach (var m in ctx.TopMovers)
            {
                var direction = m.PctChange > 0 ? "+" : "";
                userPrompt.AppendLine($"- {m.Ticker}: {direction}{m.PctChange:F2}% (split-adjusted close ${m.TodayClose:F2})");
            }
            userPrompt.AppendLine();
        }
        else
        {
            userPrompt.AppendLine("No previous market data available (this is the first trading day).");
            userPrompt.AppendLine();
        }

        if (playerCtx is not null && playerCtx.Portfolio.Holdings.Count > 0 && marketInterestingness < 0.4)
        {
            var emphasisLevel = marketInterestingness >= 0.2
                ? "subtly — give slightly more coverage to sectors and tickers the reader holds, but keep the market story primary"
                : "noticeably — today's market is quiet, so lean into sectors and tickers the reader holds to make the paper feel personally relevant";

            userPrompt.AppendLine($"The reader's current portfolio (for editorial emphasis: {emphasisLevel}. Do NOT address the reader directly or mention \"your portfolio\"):");
            foreach (var h in playerCtx.Portfolio.Holdings)
            {
                var weight = playerCtx.Portfolio.TotalHoldingsValue > 0
                    ? h.MarketValue / playerCtx.Portfolio.TotalHoldingsValue * 100 : 0;
                userPrompt.AppendLine($"- {h.TickerId}: {weight:F0}% of portfolio, {h.UnrealizedPnlPct:+0.0;-0.0}% unrealized");
            }
            userPrompt.AppendLine($"- Cash: {playerCtx.Portfolio.CashRatioPct:F0}% of net worth");
            userPrompt.AppendLine();
        }

        userPrompt.AppendLine("Generate today's edition of The Market Tribune.");

        var requestBody = new
        {
            model = _model,
            max_tokens = 1024,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt.ToString() }
            }
        };

        var jsonRequest = JsonSerializer.Serialize(requestBody);
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var response = await _httpClient.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Claude API error ({response.StatusCode}): {responseJson}");

        using var doc = JsonDocument.Parse(responseJson);
        var textBlock = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString()
            ?? throw new InvalidOperationException("Empty response from Claude API.");

        var cleaned = textBlock.Trim();
        if (cleaned.StartsWith("```"))
        {
            cleaned = cleaned.Split('\n', 2).Length > 1 ? cleaned.Split('\n', 2)[1] : cleaned;
            if (cleaned.EndsWith("```"))
                cleaned = cleaned[..^3];
            cleaned = cleaned.Trim();
        }

        using var paperDoc = JsonDocument.Parse(cleaned);
        var root = paperDoc.RootElement;

        var headline = root.GetProperty("headline").GetString() ?? "The Market Tribune";
        var marketRecap = root.GetProperty("market_recap").GetString() ?? "";

        var articles = new List<NewspaperArticleDto>();
        foreach (var a in root.GetProperty("articles").EnumerateArray())
        {
            articles.Add(new NewspaperArticleDto(
                a.GetProperty("title").GetString() ?? "",
                a.GetProperty("body").GetString() ?? "",
                a.GetProperty("category").GetString() ?? "markets"
            ));
        }

        return new NewspaperResponse("ok", date, dateDisplay, headline, articles, marketRecap, false);
    }

    public List<MoverDto> GetTopMoversForDate(string date)
    {
        using var conn = _db.Open();
        return GetTopMovers(conn, date);
    }

    private string? GetArcToneForDate(string date)
    {
        if (_arcService is null) return null;
        var allArcs = _arcService.GetAllArcs();
        var arc = allArcs.FirstOrDefault(a =>
            string.Compare(date, a.StartDate, StringComparison.Ordinal) >= 0 &&
            string.Compare(date, a.EndDate, StringComparison.Ordinal) <= 0);
        return arc?.NewspaperTone;
    }

    private record HistoricalEvent(
        [property: JsonPropertyName("date")] string Date,
        [property: JsonPropertyName("headline")] string Headline,
        [property: JsonPropertyName("summary")] string Summary,
        [property: JsonPropertyName("category")] string Category,
        [property: JsonPropertyName("related_tickers")] List<string>? RelatedTickers
    );

    public record MoverDto(string Ticker, double PrevClose, double TodayClose, double PctChange);

    private record NewspaperContext(
        string Date,
        DateOnly GameDate,
        List<HistoricalEvent> NearbyEvents,
        List<MoverDto> TopMovers,
        string? PreviousDate
    );
}
