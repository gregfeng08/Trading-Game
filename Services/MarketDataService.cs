using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TradingGame.Data;
using TradingGame.Models;

namespace TradingGame.Services;

public class MarketDataService
{
    private readonly Database _db;
    private HttpClient _http;

    private string? _crumb;
    private CookieContainer? _cookies;

    private static readonly string[] DefaultUniverse =
    [
        "AAPL","MSFT","NVDA","AMZN","GOOGL","META","TSLA","BRK-B","JPM","V",
        "XOM","UNH","PG","MA","HD","COST","AVGO","LLY","KO","PEP",
        "MRK","ABBV","ORCL","WMT","ADBE","CRM","BAC","CVX","NFLX","AMD",
        "INTC","CSCO","TMO","MCD","NKE","LIN","ACN","WFC","DHR","VZ",
        "CMCSA","DIS","QCOM","TXN","PM","NEE","RTX","HON","IBM","CAT",
        "SPGI","GE","AMGN","INTU","ISRG","BKNG","AXP","SYK","PLD","BLK",
        "MDLZ","GILD","ADI","LRCX","MMC","CB","SCHW","VRTX","REGN","ETN",
        "MO","ZTS","PANW","KLAC","CI","SO","CME","BSX","DUK","HUM",
        "CL","ITW","SHW","PGR","ICE","SNPS","CDNS","EQIX","MCK","GD",
        "MSI","PYPL","USB","PNC","AON","NOC","APD","TGT","EMR","ORLY",
        "AJG","TFC","ROP","CTAS","ECL","ADSK","NSC","SLB","WM","FDX",
        "COF","GM","JCI","TT","SPG","AFL","AEP","CARR","PSA","HLT",
        "OXY","KMB","MPC","F","SRE","AIG","D","FTNT","ALL","CCI",
        "WELL","GIS","PSX","MCHP","DHI","LHX","KMI","AMP","TEL","FAST",
        "KR","BK","CTSH","CMI","PAYX","EA","MSCI","A","OTIS","IQV",
        "EW","PRU","MNST","YUM","GLW","VRSK","HPQ","RSG","AME","KEYS",
        "IDXX","PEG","EXC","XEL","IT","DD","STZ","ED","WEC","FANG",
        "GEHC","EL","MTD","FIS","RMD","DOV","WTW","CBRE","WAB","ANSS",
        "HIG","GPN","AWK","ODFL","CHD","WY","EFX","BR","DTE","TSCO",
        "BAX","IFF","PPG","CDW","AVB","LUV","ROK","TRGP","CPRT","WBA",
        "HAL","FTV","EQR","VICI","AEE","ES","LYB","LH","STT","STE",
        "INVH","VMC","MLM","IR","K","GPC","SBAC","NTRS","RJF","PKI",
        "MAA","TER","BALL","TRMB","TYL","COO","WST","DGX","HOLX","ALGN",
        "SWK","MKC","CINF","J","POOL","FICO","SNA","BRO","IEX","JBHT",
        "LDOS","TXT","PFG","DPZ","CLX","ATO","CNP","NI","NDSN","PODD",
    ];

    public MarketDataService(Database db)
    {
        _db = db;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    }

    // ── Query endpoints ──

    public TickerListResponse GetTickers()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ticker_id, company_name, description FROM loaded_ticker_list ORDER BY ticker_id;";

        var tickers = new List<TickerDto>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tickers.Add(new TickerDto(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)
            ));
        }

        return new TickerListResponse("ok", tickers);
    }

    public PricesResponse GetPrices(string tickerId, string? startDate, string? endDate)
    {
        var tid = tickerId.ToUpperInvariant().Trim();
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();

        if (startDate is not null && endDate is not null)
        {
            cmd.CommandText = """
                SELECT ticker_id, date, open_price, high_price, low_price, close_price
                FROM ticker_prices
                WHERE ticker_id = @tid AND date BETWEEN @start AND @end
                ORDER BY date;
                """;
            cmd.Parameters.AddWithValue("@tid", tid);
            cmd.Parameters.AddWithValue("@start", startDate);
            cmd.Parameters.AddWithValue("@end", endDate);
        }
        else if (endDate is not null)
        {
            cmd.CommandText = """
                SELECT ticker_id, date, open_price, high_price, low_price, close_price
                FROM ticker_prices
                WHERE ticker_id = @tid AND date <= @end
                ORDER BY date;
                """;
            cmd.Parameters.AddWithValue("@tid", tid);
            cmd.Parameters.AddWithValue("@end", endDate);
        }
        else if (startDate is not null)
        {
            cmd.CommandText = """
                SELECT ticker_id, date, open_price, high_price, low_price, close_price
                FROM ticker_prices
                WHERE ticker_id = @tid AND date >= @start
                ORDER BY date;
                """;
            cmd.Parameters.AddWithValue("@tid", tid);
            cmd.Parameters.AddWithValue("@start", startDate);
        }
        else
        {
            cmd.CommandText = """
                SELECT ticker_id, date, open_price, high_price, low_price, close_price
                FROM ticker_prices WHERE ticker_id = @tid ORDER BY date;
                """;
            cmd.Parameters.AddWithValue("@tid", tid);
        }

        return new PricesResponse("ok", tid, ReadPriceRows(cmd));
    }

    public DailyDataResponse GetDailyData(string? ticker, int? limit)
    {
        int lim = limit ?? 500;
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();

        if (ticker is not null)
        {
            cmd.CommandText = """
                SELECT ticker_id, date, open_price, high_price, low_price, close_price
                FROM ticker_prices WHERE ticker_id = @tid
                ORDER BY date DESC LIMIT @lim;
                """;
            cmd.Parameters.AddWithValue("@tid", ticker.ToUpperInvariant().Trim());
            cmd.Parameters.AddWithValue("@lim", lim);
        }
        else
        {
            cmd.CommandText = """
                SELECT ticker_id, date, open_price, high_price, low_price, close_price
                FROM ticker_prices ORDER BY date DESC LIMIT @lim;
                """;
            cmd.Parameters.AddWithValue("@lim", lim);
        }

        var data = new List<DailyTickerDto>();
        var stubTech = new TechnicalDataDto(0f, 0f, 0f);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            data.Add(new DailyTickerDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? 0f : (float)reader.GetDouble(2),
                reader.IsDBNull(3) ? 0f : (float)reader.GetDouble(3),
                reader.IsDBNull(4) ? 0f : (float)reader.GetDouble(4),
                reader.IsDBNull(5) ? 0f : (float)reader.GetDouble(5),
                0f,
                stubTech
            ));
        }

        return new DailyDataResponse(data);
    }

    // ── Yahoo Finance download (replaces ticker_download.py) ──

    public async Task<LoadTickerDataResponse> LoadTickersAsync(string startDate, string endDate, int topN)
    {
        var symbols = DefaultUniverse.Take(topN > 0 ? topN : DefaultUniverse.Length).ToArray();

        using var conn = _db.Open();

        foreach (var sym in symbols)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO loaded_ticker_list (ticker_id, company_name, description) VALUES (@tid, NULL, NULL);";
            cmd.Parameters.AddWithValue("@tid", sym);
            cmd.ExecuteNonQuery();
        }

        var startUnix = new DateTimeOffset(DateTime.Parse(startDate, CultureInfo.InvariantCulture)).ToUnixTimeSeconds();
        var endUnix = new DateTimeOffset(DateTime.Parse(endDate, CultureInfo.InvariantCulture).AddDays(1)).ToUnixTimeSeconds();

        await EnsureYahooSessionAsync();

        int totalInserted = 0;
        int batchSize = 5;

        for (int i = 0; i < symbols.Length; i += batchSize)
        {
            var batch = symbols.Skip(i).Take(batchSize).ToArray();
            var tasks = batch.Select(sym => DownloadSymbolAsync(sym, startUnix, endUnix));
            var results = await Task.WhenAll(tasks);

            using var tx = conn.BeginTransaction();
            foreach (var (sym, rows) in batch.Zip(results))
            {
                if (rows is null) continue;
                foreach (var row in rows)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "INSERT OR IGNORE INTO ticker_prices (ticker_id, open_price, high_price, low_price, close_price, date) VALUES (@tid, @o, @h, @l, @c, @d);";
                    cmd.Parameters.AddWithValue("@tid", sym);
                    cmd.Parameters.AddWithValue("@o", row.Open);
                    cmd.Parameters.AddWithValue("@h", row.High);
                    cmd.Parameters.AddWithValue("@l", row.Low);
                    cmd.Parameters.AddWithValue("@c", row.Close);
                    cmd.Parameters.AddWithValue("@d", row.Date);
                    cmd.ExecuteNonQuery();
                    totalInserted++;
                }
            }
            tx.Commit();
        }

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(DISTINCT ticker_id) FROM ticker_prices;";
        var tickerCount = Convert.ToInt32(countCmd.ExecuteScalar());

        return new LoadTickerDataResponse("ok", $"Loaded {totalInserted} price rows for {tickerCount} tickers", tickerCount);
    }

    public int GetTickerCount()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM loaded_ticker_list;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    // ── Yahoo Finance internals ──

    private async Task EnsureYahooSessionAsync()
    {
        if (_crumb is not null) return;

        var cookieHandler = new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true };
        using var sessionClient = new HttpClient(cookieHandler);
        sessionClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        await sessionClient.GetAsync("https://fc.yahoo.com/");

        var crumbResponse = await sessionClient.GetAsync("https://query2.finance.yahoo.com/v1/test/getcrumb");
        crumbResponse.EnsureSuccessStatusCode();
        _crumb = await crumbResponse.Content.ReadAsStringAsync();
        _cookies = cookieHandler.CookieContainer;

        var newHandler = new HttpClientHandler { CookieContainer = _cookies, UseCookies = true };
        _http = new HttpClient(newHandler);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    }

    private async Task<List<OhlcRow>?> DownloadSymbolAsync(string symbol, long period1, long period2)
    {
        var url = $"https://query2.finance.yahoo.com/v8/finance/chart/{symbol}?period1={period1}&period2={period2}&interval=1d&crumb={Uri.EscapeDataString(_crumb!)}";

        try
        {
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var result = doc.RootElement
                .GetProperty("chart")
                .GetProperty("result")[0];

            var timestamps = result.GetProperty("timestamp");
            var quote = result.GetProperty("indicators").GetProperty("quote")[0];

            var opens = quote.GetProperty("open");
            var highs = quote.GetProperty("high");
            var lows = quote.GetProperty("low");
            var closes = quote.GetProperty("close");

            var rows = new List<OhlcRow>();
            for (int i = 0; i < timestamps.GetArrayLength(); i++)
            {
                if (IsJsonNull(opens[i]) || IsJsonNull(highs[i]) || IsJsonNull(lows[i]) || IsJsonNull(closes[i]))
                    continue;

                var dt = DateTimeOffset.FromUnixTimeSeconds(timestamps[i].GetInt64()).UtcDateTime;
                rows.Add(new OhlcRow(
                    dt.ToString("yyyy-MM-dd"),
                    opens[i].GetDouble(),
                    highs[i].GetDouble(),
                    lows[i].GetDouble(),
                    closes[i].GetDouble()
                ));
            }

            return rows;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsJsonNull(JsonElement el) =>
        el.ValueKind == JsonValueKind.Null;

    private record OhlcRow(string Date, double Open, double High, double Low, double Close);

    // ── Shared helpers ──

    private static List<PriceRowDto> ReadPriceRows(SqliteCommand cmd)
    {
        var rows = new List<PriceRowDto>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new PriceRowDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? 0.0 : reader.GetDouble(2),
                reader.IsDBNull(3) ? 0.0 : reader.GetDouble(3),
                reader.IsDBNull(4) ? 0.0 : reader.GetDouble(4),
                reader.IsDBNull(5) ? 0.0 : reader.GetDouble(5)
            ));
        }
        return rows;
    }
}
