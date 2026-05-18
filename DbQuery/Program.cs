using Microsoft.Data.Sqlite;

var dbPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Data", "database.db"));
Console.WriteLine($"Database: {dbPath}");
Console.WriteLine($"Exists: {File.Exists(dbPath)}");
Console.WriteLine();

using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
conn.Open();

// 1. Basic stats
Console.WriteLine("=== DATABASE STATS ===");
Query(conn, "SELECT COUNT(*) FROM ticker_prices;", "Total price rows");
Query(conn, "SELECT COUNT(DISTINCT ticker_id) FROM ticker_prices;", "Distinct tickers");
Query(conn, "SELECT MIN(date), MAX(date) FROM ticker_prices;", "Date range");
Console.WriteLine();

// 2. Actual prices on game start date (2010-01-04)
Console.WriteLine("=== PRICES ON 2010-01-04 (Game Start) ===");
QueryAll(conn, """
    SELECT ticker_id, open_price, high_price, low_price, close_price
    FROM ticker_prices WHERE date = '2010-01-04'
    ORDER BY ticker_id LIMIT 30;
""");
Console.WriteLine();

// 3. Previous trading day
Console.WriteLine("=== PREVIOUS TRADING DAY ===");
Query(conn, "SELECT MAX(date) FROM ticker_prices WHERE date < '2010-01-04';", "Previous date");
var prevDate = QueryScalar(conn, "SELECT MAX(date) FROM ticker_prices WHERE date < '2010-01-04';");
Console.WriteLine();

// 4. Top movers on 2010-01-04 vs previous day
if (prevDate != null)
{
    Console.WriteLine($"=== TOP MOVERS: 2010-01-04 vs {prevDate} ===");
    QueryAll(conn, $"""
        SELECT t.ticker_id,
               p.close_price as prev_close,
               t.close_price as today_close,
               ROUND((t.close_price - p.close_price) / p.close_price * 100, 2) as pct_change
        FROM ticker_prices t
        JOIN ticker_prices p ON t.ticker_id = p.ticker_id AND p.date = '{prevDate}'
        WHERE t.date = '2010-01-04' AND p.close_price > 0
        ORDER BY pct_change DESC
        LIMIT 10;
    """);
    Console.WriteLine();

    Console.WriteLine($"=== BIGGEST LOSERS: 2010-01-04 vs {prevDate} ===");
    QueryAll(conn, $"""
        SELECT t.ticker_id,
               p.close_price as prev_close,
               t.close_price as today_close,
               ROUND((t.close_price - p.close_price) / p.close_price * 100, 2) as pct_change
        FROM ticker_prices t
        JOIN ticker_prices p ON t.ticker_id = p.ticker_id AND p.date = '{prevDate}'
        WHERE t.date = '2010-01-04' AND p.close_price > 0
        ORDER BY pct_change ASC
        LIMIT 10;
    """);
    Console.WriteLine();
}

// 5. Specific tickers I used in the test newspaper
Console.WriteLine("=== VERIFY TEST NEWSPAPER TICKERS ===");
foreach (var ticker in new[] { "JPM", "BAC", "AAPL", "MSFT", "GS", "NFLX", "HPQ", "INTC", "XOM", "WFC" })
{
    QueryAll(conn, $"""
        SELECT ticker_id, date, close_price
        FROM ticker_prices
        WHERE ticker_id = '{ticker}' AND date IN ('2010-01-04', '{prevDate}')
        ORDER BY date;
    """);
}

// 6. Check tickers loaded
Console.WriteLine("=== LOADED TICKERS ===");
QueryAll(conn, "SELECT ticker_id, company_name FROM loaded_ticker_list ORDER BY ticker_id LIMIT 30;");

static void Query(SqliteConnection conn, string sql, string label)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    using var reader = cmd.ExecuteReader();
    if (reader.Read())
    {
        var vals = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
            vals.Add(reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString()!);
        Console.WriteLine($"  {label}: {string.Join(" | ", vals)}");
    }
}

static string? QueryScalar(SqliteConnection conn, string sql)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    var result = cmd.ExecuteScalar();
    return result is not null and not DBNull ? result.ToString() : null;
}

static void QueryAll(SqliteConnection conn, string sql)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    using var reader = cmd.ExecuteReader();

    // Header
    var cols = new List<string>();
    for (int i = 0; i < reader.FieldCount; i++)
        cols.Add(reader.GetName(i));
    Console.WriteLine($"  {string.Join(" | ", cols)}");
    Console.WriteLine($"  {new string('-', cols.Count * 15)}");

    while (reader.Read())
    {
        var vals = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
            vals.Add(reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString()!);
        Console.WriteLine($"  {string.Join(" | ", vals)}");
    }
}
