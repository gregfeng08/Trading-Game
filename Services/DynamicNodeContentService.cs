using System.Text;
using System.Text.Json;
using TradingGame.Data;
using TradingGame.Models;

namespace TradingGame.Services;

public class DynamicNodeContentService
{
    private readonly Database _db;
    private readonly PlayerContextService _playerContext;
    private readonly KnowledgeGraphService _knowledgeGraph;
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _model;

    public DynamicNodeContentService(Database db, PlayerContextService playerContext,
        KnowledgeGraphService knowledgeGraph)
    {
        _db = db;
        _playerContext = playerContext;
        _knowledgeGraph = knowledgeGraph;
        _httpClient = new HttpClient();
        _apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        _model = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL") ?? "claude-sonnet-4-20250514";
    }

    public async Task<string?> GeneratePersonalizedContent(
        int entityId, string nodeId, string gameDate, string? triggerDescription)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return null;

        var node = _knowledgeGraph.Config.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null)
            return null;

        var cached = GetCachedContent(entityId, nodeId);
        if (cached is not null)
            return cached;

        try
        {
            var ctx = _playerContext.BuildContext(entityId, gameDate, "post_market");

            var playerSummary = new StringBuilder();
            playerSummary.AppendLine($"Cash: ${ctx.Portfolio.Cash:F2}");
            playerSummary.AppendLine($"Net worth: ${ctx.Portfolio.NetWorth:F2}");
            if (ctx.Portfolio.Holdings.Count > 0)
            {
                playerSummary.AppendLine("Current holdings:");
                foreach (var h in ctx.Portfolio.Holdings)
                    playerSummary.AppendLine($"  {h.TickerId}: {h.SharesHeld:F0} shares, avg cost ${h.AvgCostBasis:F2}, now ${h.CurrentPrice:F2} ({h.UnrealizedPnlPct:+0.0;-0.0}%)");
            }
            if (ctx.Concentration.MaxTicker is not null)
                playerSummary.AppendLine($"Portfolio concentration: {ctx.Concentration.MaxSingleTickerPct:F0}% in {ctx.Concentration.MaxTicker}");
            playerSummary.AppendLine($"Cash ratio: {ctx.Portfolio.CashRatioPct:F0}%");
            playerSummary.AppendLine($"Total trades: {ctx.RecentTrades.TotalTradeCount}");
            if (ctx.Knowledge.CompletedCount > 0)
                playerSummary.AppendLine($"Learning progress: {ctx.Knowledge.CompletedCount}/{ctx.Knowledge.TotalNodes} concepts completed");

            var systemPrompt = """
                You are a financial educator within a trading simulation game. A player just triggered
                a learning moment through their own trading behavior. Write a personalized explanation
                of this financial concept, connecting it directly to what the player did.

                RULES:
                - Write 2-3 short paragraphs. Conversational, clear, educational.
                - Reference the player's specific situation (their holdings, trades, concentration, etc.).
                - Explain WHY this concept matters for THEIR portfolio, not just in general.
                - Do not lecture. The player just experienced this firsthand — help them understand what happened.
                - Do not reference game mechanics, UI elements, or that this is a game.
                - Write as a knowledgeable mentor explaining to a trainee.
                """;

            var userPrompt = $"""
                CONCEPT: {node.Title}
                STANDARD EXPLANATION: {node.Content}
                TRIGGER: {triggerDescription ?? node.TriggerExplanation ?? "Player behavior triggered this lesson"}

                PLAYER'S CURRENT SITUATION:
                {playerSummary}

                Write a personalized explanation that connects this concept to their actual trades and portfolio.
                """;

            var requestBody = new
            {
                model = _model,
                max_tokens = 768,
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
                Console.WriteLine($"[DynamicContent] Claude API error for {nodeId}: {response.StatusCode}");
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var content = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            if (content is null) return null;

            CacheContent(entityId, nodeId, content, triggerDescription);
            return content;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DynamicContent] Generation failed for {nodeId}: {ex.Message}");
            return null;
        }
    }

    public string? GetCachedContent(int entityId, string nodeId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT content FROM dynamic_node_content WHERE entity_id = @eid AND node_id = @nid;";
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@nid", nodeId);
        var result = cmd.ExecuteScalar();
        return result is not null and not DBNull ? (string)result : null;
    }

    public async Task<string> GetContentWithFallback(int entityId, string nodeId, string gameDate)
    {
        var cached = GetCachedContent(entityId, nodeId);
        if (cached is not null) return cached;

        var generated = await GeneratePersonalizedContent(entityId, nodeId, gameDate, null);
        if (generated is not null) return generated;

        var node = _knowledgeGraph.Config.Nodes.FirstOrDefault(n => n.Id == nodeId);
        return node?.Content ?? "";
    }

    private void CacheContent(int entityId, string nodeId, string content, string? triggerContext)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO dynamic_node_content (entity_id, node_id, content, trigger_context, generated_at)
            VALUES (@eid, @nid, @content, @trigger, @at)
            ON CONFLICT(entity_id, node_id) DO UPDATE SET
                content = excluded.content, trigger_context = excluded.trigger_context,
                generated_at = excluded.generated_at;
            """;
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@nid", nodeId);
        cmd.Parameters.AddWithValue("@content", content);
        cmd.Parameters.AddWithValue("@trigger", (object?)triggerContext ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@at", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }
}
