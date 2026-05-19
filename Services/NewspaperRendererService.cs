using System.Net;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using TradingGame.Models;

namespace TradingGame.Services;

public class NewspaperRendererService : IAsyncDisposable
{
    private readonly string _templateHtml;
    private readonly NewspaperService _newspaper;
    private readonly ArcService? _arcService;
    private readonly string _cachePath;
    private IBrowser? _browser;
    private readonly SemaphoreSlim _browserLock = new(1, 1);

    public NewspaperRendererService(NewspaperService newspaper, string templatePath, string cachePath, ArcService? arcService = null)
    {
        _newspaper = newspaper;
        _arcService = arcService;
        _templateHtml = File.ReadAllText(templatePath);
        _cachePath = cachePath;
        Directory.CreateDirectory(_cachePath);
    }

    public async Task<byte[]> GetNewspaperImage(string? date)
    {
        var paper = await _newspaper.GetNewspaper(date);
        var cacheFile = Path.Combine(_cachePath, $"{paper.Date}.png");

        if (File.Exists(cacheFile))
            return await File.ReadAllBytesAsync(cacheFile);

        var html = BuildHtml(paper);
        var png = await RenderHtmlToPng(html);

        await File.WriteAllBytesAsync(cacheFile, png);
        return png;
    }

    private string BuildHtml(NewspaperResponse paper)
    {
        var html = _templateHtml;
        var gameDate = DateOnly.ParseExact(paper.Date, "yyyy-MM-dd");

        var arcName = "";
        if (_arcService is not null)
        {
            var arc = _arcService.GetAllArcs().FirstOrDefault(a =>
                string.Compare(paper.Date, a.StartDate, StringComparison.Ordinal) >= 0 &&
                string.Compare(paper.Date, a.EndDate, StringComparison.Ordinal) <= 0);
            arcName = arc?.Name ?? "";
        }

        var daysSinceEpoch = gameDate.DayNumber - new DateOnly(2007, 1, 1).DayNumber;
        var volume = (daysSinceEpoch / 365) + 1;
        var issue = (daysSinceEpoch % 365) + 1;

        html = html.Replace("{{DATE_DISPLAY}}", Encode(paper.DateDisplay));
        html = html.Replace("{{ARC_NAME}}", Encode(arcName));
        html = html.Replace("{{VOLUME}}", volume.ToString());
        html = html.Replace("{{ISSUE}}", issue.ToString());
        html = html.Replace("{{HEADLINE}}", Encode(paper.Headline));
        html = html.Replace("{{MARKET_RECAP}}", Encode(paper.MarketRecap));

        for (int i = 0; i < 3; i++)
        {
            if (i < paper.Articles.Count)
            {
                html = html.Replace($"{{{{ARTICLE_{i}_CATEGORY}}}}", Encode(paper.Articles[i].Category));
                html = html.Replace($"{{{{ARTICLE_{i}_TITLE}}}}", Encode(paper.Articles[i].Title));
                html = html.Replace($"{{{{ARTICLE_{i}_BODY}}}}", Encode(paper.Articles[i].Body));
            }
            else
            {
                html = html.Replace($"{{{{ARTICLE_{i}_CATEGORY}}}}", "");
                html = html.Replace($"{{{{ARTICLE_{i}_TITLE}}}}", "");
                html = html.Replace($"{{{{ARTICLE_{i}_BODY}}}}", "");
            }
        }

        var movers = _newspaper.GetTopMoversForDate(paper.Date);
        var gainers = movers.Where(m => m.PctChange > 0).OrderByDescending(m => m.PctChange).Take(5);
        var losers = movers.Where(m => m.PctChange < 0).OrderBy(m => m.PctChange).Take(5);

        html = html.Replace("{{GAINERS_ROWS}}", BuildMoverRows(gainers, positive: true));
        html = html.Replace("{{LOSERS_ROWS}}", BuildMoverRows(losers, positive: false));

        return html;
    }

    private static string BuildMoverRows(IEnumerable<NewspaperService.MoverDto> movers, bool positive)
    {
        var cssClass = positive ? "positive" : "negative";
        var rows = movers.Select(m =>
        {
            var sign = m.PctChange > 0 ? "+" : "";
            return $"""
                <div class="mover-row">
                    <span class="mover-ticker">{Encode(m.Ticker)}</span>
                    <span class="mover-change {cssClass}">{sign}{m.PctChange:F2}%</span>
                </div>
                """;
        });
        return string.Join("\n", rows);
    }

    private async Task<byte[]> RenderHtmlToPng(string html)
    {
        var browser = await GetBrowser();
        await using var page = await browser.NewPageAsync();
        await page.SetViewportAsync(new ViewPortOptions { Width = 800, Height = 1100 });
        await page.SetContentAsync(html, new NavigationOptions { WaitUntil = [WaitUntilNavigation.Networkidle0] });

        await Task.Delay(500);

        var bodyHandle = await page.QuerySelectorAsync("body");
        var boundingBox = await bodyHandle!.BoundingBoxAsync();
        var height = (int)Math.Ceiling(boundingBox?.Height ?? 1100);

        return await page.ScreenshotDataAsync(new ScreenshotOptions
        {
            Type = ScreenshotType.Png,
            Clip = new Clip { X = 0, Y = 0, Width = 800, Height = height }
        });
    }

    private async Task<IBrowser> GetBrowser()
    {
        if (_browser is not null) return _browser;

        await _browserLock.WaitAsync();
        try
        {
            if (_browser is not null) return _browser;

            var fetcher = new BrowserFetcher();
            await fetcher.DownloadAsync();
            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-gpu"]
            });
            return _browser;
        }
        finally
        {
            _browserLock.Release();
        }
    }

    private static string Encode(string text) => WebUtility.HtmlEncode(text);

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
            _browser = null;
        }
        _browserLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
