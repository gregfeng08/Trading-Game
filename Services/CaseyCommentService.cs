using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TradingGame.Data;
using TradingGame.Models;

namespace TradingGame.Services;

public class CaseyCommentService
{
    private readonly Database _db;
    private readonly PlayerContextService _playerContext;
    private readonly KnowledgeGraphService _knowledgeGraph;
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _model;

    public CaseyCommentService(Database db, PlayerContextService playerContext,
        KnowledgeGraphService knowledgeGraph)
    {
        _db = db;
        _playerContext = playerContext;
        _knowledgeGraph = knowledgeGraph;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        _model = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL") ?? "claude-sonnet-4-20250514";
    }

    public async Task<string?> GenerateComment(int entityId, string gameDate)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return null;

        var cached = GetCached(entityId, gameDate);
        if (cached is not null)
            return cached;

        try
        {
            var ctx = _playerContext.BuildContext(entityId, gameDate, "post_market");

            var daySummary = new StringBuilder();

            // Today's trades
            if (ctx.RecentTrades.TodaysTrades.Count > 0)
            {
                daySummary.AppendLine("Trades today:");
                foreach (var t in ctx.RecentTrades.TodaysTrades)
                {
                    var side = t.Shares > 0 ? "BOUGHT" : "SOLD";
                    daySummary.AppendLine($"  {side} {Math.Abs(t.Shares):F0} {t.TickerId} @ ${t.Price:F2}");
                }
            }
            else
            {
                daySummary.AppendLine("No trades today.");
            }

            // Portfolio state
            daySummary.AppendLine($"\nCash: ${ctx.Portfolio.Cash:F2}");
            daySummary.AppendLine($"Net worth: ${ctx.Portfolio.NetWorth:F2}");
            daySummary.AppendLine($"Cash ratio: {ctx.Portfolio.CashRatioPct:F0}%");

            if (ctx.Portfolio.Holdings.Count > 0)
            {
                daySummary.AppendLine("Holdings:");
                foreach (var h in ctx.Portfolio.Holdings)
                    daySummary.AppendLine($"  {h.TickerId}: {h.SharesHeld:F0} shares ({h.UnrealizedPnlPct:+0.0;-0.0}%)");
            }

            if (ctx.Concentration.MaxTicker is not null)
                daySummary.AppendLine($"Concentration: {ctx.Concentration.MaxSingleTickerPct:F0}% in {ctx.Concentration.MaxTicker}");

            // Market context
            if (ctx.Market.TopGainers.Count > 0 || ctx.Market.TopLosers.Count > 0)
            {
                daySummary.AppendLine($"\nMarket: {ctx.Market.AdvancingCount} advancing, {ctx.Market.DecliningCount} declining");
            }

            // Knowledge state
            if (ctx.Knowledge.RecentlyUnlocked.Count > 0)
                daySummary.AppendLine($"\nNew insights unlocked today: {string.Join(", ", ctx.Knowledge.RecentlyUnlocked.Select(n => n.Title))}");

            // Arc context
            daySummary.AppendLine($"\nArc: {ctx.Arc.ArcName ?? "Unknown"} ({ctx.Arc.ArcTone ?? "neutral"})");
            daySummary.AppendLine($"Arc return so far: {ctx.Arc.ProjectedReturnPct:+0.0;-0.0}%");
            daySummary.AppendLine($"Days remaining in arc: {ctx.Arc.DaysRemaining}");

            var systemPrompt = """
                You are Casey, a mentor at Hindsight Financial — a training institution where
                analysts relive historical market periods. You're reviewing a trainee's day.

                VOICE:
                - Casual, warm, uses contractions. Never condescending.
                - Honest about mistakes and pressure. Slightly harried, clearly human.
                - You genuinely care about this trainee's development.
                - Short and punchy — you're adding a quick closing thought, not writing an essay.

                RULES:
                - Write exactly ONE sentence. Two at absolute most if the day was exceptional.
                - React to what actually happened today — their trades, their P&L, their risk.
                - If they didn't trade, acknowledge that. If they took a big loss, be empathetic
                  but frame it as learning. If they did well, congratulate but keep them grounded.
                - Match the arc tone. Bull market = more casual. Unraveling = more cautious.
                  Meltdown = more serious, acknowledge the difficulty.
                - Do NOT reference that this is a game or simulation.
                - Do NOT give specific trading advice ("you should buy X" or "sell Y").
                - Do NOT reference future events. You are living on this date.
                - Do NOT use exclamation marks excessively. One per response maximum.
                """;

            var userPrompt = $"""
                Date: {gameDate}

                {daySummary}

                Write Casey's one-sentence closing comment on this trainee's day.
                """;

            var requestBody = new
            {
                model = _model,
                max_tokens = 100,
                system = systemPrompt,
                messages = new[] { new { role = "user", content = userPrompt } }
            };

            var jsonRequest = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[CaseyComment] Claude API error: {response.StatusCode}");
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var content = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            if (content is null) return null;

            content = content.Trim().Trim('"');
            Cache(entityId, gameDate, content);
            return content;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CaseyComment] Generation failed: {ex.Message}");
            return null;
        }
    }

    private string? GetCached(int entityId, string gameDate)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT comment FROM casey_daily_comments WHERE entity_id = @eid AND date = @date;";
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@date", gameDate);
        var result = cmd.ExecuteScalar();
        return result is not null and not DBNull ? (string)result : null;
    }

    private void Cache(int entityId, string gameDate, string comment)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO casey_daily_comments (entity_id, date, comment, generated_at)
            VALUES (@eid, @date, @comment, datetime('now'));
            """;
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@date", gameDate);
        cmd.Parameters.AddWithValue("@comment", comment);
        cmd.ExecuteNonQuery();
    }
}
