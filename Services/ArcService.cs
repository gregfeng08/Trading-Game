using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using TradingGame.Data;
using TradingGame.Models;

namespace TradingGame.Services;

public class ArcService
{
    private readonly Database _db;
    private readonly EntityService _entities;
    private readonly GameStateService _gameState;
    private readonly List<ArcConfig> _arcs;

    public ArcService(Database db, EntityService entities, GameStateService gameState, string arcsPath)
    {
        _db = db;
        _entities = entities;
        _gameState = gameState;

        var json = File.ReadAllText(arcsPath);
        var config = JsonSerializer.Deserialize<ArcFileConfig>(json)!;
        _arcs = config.Arcs.OrderBy(a => a.StartDate).ToList();
    }

    public ArcStatusResponse GetStatus(string externalId)
    {
        using var conn = _db.Open();
        var currentDate = _gameState.GetSaveValue(conn, "current_date");
        if (currentDate is null)
            return new ArcStatusResponse("ok", "", null, null, null, null, "No active game.");

        var entityDbId = _entities.ResolveExternalId(conn, externalId);
        if (entityDbId is null)
            return new ArcStatusResponse("error", currentDate, null, null, null, null, $"Entity '{externalId}' not found");

        var arc = GetArcForDate(currentDate);
        if (arc is null)
            return new ArcStatusResponse("ok", currentDate, null, null, null, null, "No active arc for current date");

        EnsureArcStartSnapshot(conn, entityDbId.Value, arc, currentDate);

        var daysRemaining = CountTradingDaysRemaining(conn, currentDate, arc.EndDate);
        var startValue = GetArcStartValue(conn, entityDbId.Value, arc.Id);
        double? returnPct = null;
        string? projectedGrade = null;

        if (startValue.HasValue && startValue.Value > 0)
        {
            var currentValue = CalculatePortfolioValue(conn, entityDbId.Value, currentDate);
            returnPct = Math.Round((currentValue - startValue.Value) / startValue.Value * 100.0, 2);
            projectedGrade = DetermineGrade(arc, returnPct.Value);
        }

        return new ArcStatusResponse("ok", currentDate, ToDefinitionDto(arc), daysRemaining, returnPct, projectedGrade, null);
    }

    public ArcAdvanceResponse CheckAdvance(string externalId)
    {
        using var conn = _db.Open();
        var currentDate = _gameState.GetSaveValue(conn, "current_date")
            ?? throw new InvalidOperationException("No active game.");

        var entityDbId = _entities.ResolveExternalId(conn, externalId);
        if (entityDbId is null)
            throw new InvalidOperationException($"Entity '{externalId}' not found");

        var trackedArcId = _gameState.GetSaveValue(conn, "current_arc");
        var currentArc = GetArcForDate(currentDate);
        var currentArcId = currentArc?.Id;

        if (trackedArcId == currentArcId)
            return new ArcAdvanceResponse("ok", null, "No arc transition");

        if (trackedArcId is null)
        {
            if (currentArc is not null)
            {
                EnsureArcStartSnapshot(conn, entityDbId.Value, currentArc, currentDate);
                _gameState.SetSaveValue(conn, "current_arc", currentArc.Id);
            }
            return new ArcAdvanceResponse("ok", null, currentArc is not null ? $"Entered arc: {currentArc.Name}" : "No active arc");
        }

        var completedArc = _arcs.FirstOrDefault(a => a.Id == trackedArcId);
        if (completedArc is null)
        {
            _gameState.SetSaveValue(conn, "current_arc", currentArcId ?? "");
            return new ArcAdvanceResponse("ok", null, "Previous arc not found in config");
        }

        var transition = CompleteArc(conn, entityDbId.Value, completedArc, currentArc, currentDate);
        _gameState.SetSaveValue(conn, "current_arc", currentArcId ?? "");

        if (currentArc is not null)
            EnsureArcStartSnapshot(conn, entityDbId.Value, currentArc, currentDate);

        return new ArcAdvanceResponse("ok", transition, null);
    }

    public ArcGradesResponse GetGrades(int entityDbId)
    {
        using var conn = _db.Open();
        var grades = new List<ArcGradeDto>();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT arc_id, start_date, end_date, start_value, end_value, return_pct, grade, cash_multiplier
            FROM arc_grades WHERE entity_id = @eid ORDER BY start_date;
            """;
        cmd.Parameters.AddWithValue("@eid", entityDbId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var arcId = reader.GetString(0);
            var arcConfig = _arcs.FirstOrDefault(a => a.Id == arcId);
            grades.Add(new ArcGradeDto(
                arcId,
                arcConfig?.Name ?? arcId,
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDouble(3),
                reader.GetDouble(4),
                reader.GetDouble(5),
                reader.GetString(6),
                reader.GetDouble(7)
            ));
        }

        return new ArcGradesResponse("ok", grades);
    }

    public List<ArcConfig> GetAllArcs() => _arcs;

    private ArcTransitionDto CompleteArc(SqliteConnection conn, int entityDbId,
        ArcConfig completedArc, ArcConfig? nextArc, string currentDate)
    {
        var lastTradingDay = GetLastTradingDayInRange(conn, completedArc.StartDate, completedArc.EndDate);
        var evalDate = lastTradingDay ?? completedArc.EndDate;

        var startValue = GetArcStartValue(conn, entityDbId, completedArc.Id) ?? 10000.0;
        var endValue = CalculatePortfolioValue(conn, entityDbId, evalDate);

        var returnPct = startValue > 0
            ? Math.Round((endValue - startValue) / startValue * 100.0, 2)
            : 0.0;

        var grade = DetermineGrade(completedArc, returnPct);
        var multiplier = completedArc.CashMultipliers.GetValueOrDefault(grade, 1.0);

        var entity = _entities.GetEntity(conn, entityDbId);
        var cashBefore = entity?.AvailableCash ?? 0;
        var cashAfter = Math.Round(cashBefore * multiplier, 2);

        using (var cashCmd = conn.CreateCommand())
        {
            cashCmd.CommandText = "UPDATE entity SET available_cash = @cash WHERE entity_id = @eid;";
            cashCmd.Parameters.AddWithValue("@cash", cashAfter);
            cashCmd.Parameters.AddWithValue("@eid", entityDbId);
            cashCmd.ExecuteNonQuery();
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO arc_grades (arc_id, entity_id, start_date, end_date, start_value, end_value, return_pct, grade, cash_multiplier, completed_at)
            VALUES (@aid, @eid, @sd, @ed, @sv, @ev, @rp, @g, @cm, @ca)
            ON CONFLICT(arc_id, entity_id) DO UPDATE SET
                end_value=excluded.end_value, return_pct=excluded.return_pct, grade=excluded.grade,
                cash_multiplier=excluded.cash_multiplier, completed_at=excluded.completed_at;
            """;
        cmd.Parameters.AddWithValue("@aid", completedArc.Id);
        cmd.Parameters.AddWithValue("@eid", entityDbId);
        cmd.Parameters.AddWithValue("@sd", completedArc.StartDate);
        cmd.Parameters.AddWithValue("@ed", completedArc.EndDate);
        cmd.Parameters.AddWithValue("@sv", startValue);
        cmd.Parameters.AddWithValue("@ev", endValue);
        cmd.Parameters.AddWithValue("@rp", returnPct);
        cmd.Parameters.AddWithValue("@g", grade);
        cmd.Parameters.AddWithValue("@cm", multiplier);
        cmd.Parameters.AddWithValue("@ca", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();

        var gradeDto = new ArcGradeDto(
            completedArc.Id, completedArc.Name, completedArc.StartDate, completedArc.EndDate,
            startValue, endValue, returnPct, grade, multiplier);

        return new ArcTransitionDto(gradeDto, nextArc is not null ? ToDefinitionDto(nextArc) : null, cashBefore, cashAfter);
    }

    private void EnsureArcStartSnapshot(SqliteConnection conn, int entityDbId, ArcConfig arc, string currentDate)
    {
        var key = $"arc_start_value_{arc.Id}_{entityDbId}";
        var existing = _gameState.GetSaveValue(conn, key);
        if (existing is not null) return;

        var value = CalculatePortfolioValue(conn, entityDbId, currentDate);
        _gameState.SetSaveValue(conn, key, value.ToString("F2"));

        if (_gameState.GetSaveValue(conn, "current_arc") is null)
            _gameState.SetSaveValue(conn, "current_arc", arc.Id);
    }

    private double? GetArcStartValue(SqliteConnection conn, int entityDbId, string arcId)
    {
        var key = $"arc_start_value_{arcId}_{entityDbId}";
        var val = _gameState.GetSaveValue(conn, key);
        return val is not null ? double.Parse(val) : null;
    }

    private double CalculatePortfolioValue(SqliteConnection conn, int entityDbId, string date)
    {
        var entity = _entities.GetEntity(conn, entityDbId);
        double cash = entity?.AvailableCash ?? 0;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT p.ticker_id, SUM(p.shares_held) as total_shares
            FROM portfolio p
            WHERE p.entity_id = @eid
            GROUP BY p.ticker_id;
            """;
        cmd.Parameters.AddWithValue("@eid", entityDbId);

        var holdings = new List<(string Ticker, double Shares)>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                holdings.Add((reader.GetString(0), reader.GetDouble(1)));
        }

        double holdingsValue = 0;
        foreach (var (ticker, shares) in holdings)
        {
            var price = GetMostRecentClosePrice(conn, ticker, date);
            if (price.HasValue)
                holdingsValue += shares * price.Value;
        }

        return Math.Round(cash + holdingsValue, 2);
    }

    private double? GetMostRecentClosePrice(SqliteConnection conn, string tickerId, string date)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT close_price FROM ticker_prices WHERE ticker_id = @tid AND date <= @d ORDER BY date DESC LIMIT 1;";
        cmd.Parameters.AddWithValue("@tid", tickerId);
        cmd.Parameters.AddWithValue("@d", date);
        var result = cmd.ExecuteScalar();
        return result is not null and not DBNull ? Convert.ToDouble(result) : null;
    }

    private string? GetLastTradingDayInRange(SqliteConnection conn, string startDate, string endDate)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT MAX(date) FROM ticker_prices WHERE date >= @s AND date <= @e;";
        cmd.Parameters.AddWithValue("@s", startDate);
        cmd.Parameters.AddWithValue("@e", endDate);
        var result = cmd.ExecuteScalar();
        return result is not null and not DBNull ? (string)result : null;
    }

    private int CountTradingDaysRemaining(SqliteConnection conn, string currentDate, string endDate)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(DISTINCT date) FROM ticker_prices WHERE date > @c AND date <= @e;";
        cmd.Parameters.AddWithValue("@c", currentDate);
        cmd.Parameters.AddWithValue("@e", endDate);
        return Convert.ToInt32(cmd.ExecuteScalar()!);
    }

    private ArcConfig? GetArcForDate(string date)
    {
        return _arcs.FirstOrDefault(a =>
            string.Compare(date, a.StartDate, StringComparison.Ordinal) >= 0 &&
            string.Compare(date, a.EndDate, StringComparison.Ordinal) <= 0);
    }

    private static string DetermineGrade(ArcConfig arc, double returnPct)
    {
        if (returnPct >= arc.GradeThresholds.GetValueOrDefault("S", double.MaxValue)) return "S";
        if (returnPct >= arc.GradeThresholds.GetValueOrDefault("A", double.MaxValue)) return "A";
        if (returnPct >= arc.GradeThresholds.GetValueOrDefault("B", double.MaxValue)) return "B";
        if (returnPct >= arc.GradeThresholds.GetValueOrDefault("C", double.MaxValue)) return "C";
        return "D";
    }

    private static ArcDefinitionDto ToDefinitionDto(ArcConfig arc) =>
        new(arc.Id, arc.Name, arc.Description, arc.StartDate, arc.EndDate, arc.NewspaperTone);

    // ── Config model ──

    public class ArcFileConfig
    {
        [JsonPropertyName("arcs")]
        public List<ArcConfig> Arcs { get; set; } = [];
    }

    public class ArcConfig
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("start_date")]
        public string StartDate { get; set; } = "";

        [JsonPropertyName("end_date")]
        public string EndDate { get; set; } = "";

        [JsonPropertyName("newspaper_tone")]
        public string NewspaperTone { get; set; } = "";

        [JsonPropertyName("grade_thresholds")]
        public Dictionary<string, double> GradeThresholds { get; set; } = new();

        [JsonPropertyName("cash_multipliers")]
        public Dictionary<string, double> CashMultipliers { get; set; } = new();
    }
}
