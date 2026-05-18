using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.API;
using Game.API.DTO;

public class TradingUIController : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject tradingPanel;
    [SerializeField] private Button closeButton;

    [Header("Ticker Selection")]
    [SerializeField] private TMP_Dropdown tickerDropdown;

    [Header("Market Info")]
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text companyNameText;
    [SerializeField] private OHLCChart ohlcChart;

    [Header("Chart Timeframe")]
    [SerializeField] private Button btn1W;
    [SerializeField] private Button btn1M;
    [SerializeField] private Button btn3M;
    [SerializeField] private Button btn1Y;
    [SerializeField] private Button btn5Y;
    [SerializeField] private Color timeframeActiveColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color timeframeInactiveColor = new Color(1f, 1f, 1f, 0.4f);

    [Header("Trade Controls")]
    [SerializeField] private TMP_InputField quantityInput;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private Button advanceDayButton;
    [SerializeField] private TMP_Text advanceButtonText;

    [Header("Player Info")]
    [SerializeField] private TMP_Text cashText;
    [SerializeField] private TMP_Text netWorthText;
    [SerializeField] private TMP_Text holdingsText;

    [Header("Portfolio Chart")]
    [SerializeField] private Button portfolioChartButton;
    [SerializeField] private Button tickerChartButton;
    [SerializeField] private PortfolioLineChart portfolioChart;
    [SerializeField] private Color chartTabActiveColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color chartTabInactiveColor = new Color(1f, 1f, 1f, 0.4f);

    [Header("Feedback")]
    [SerializeField] private TMP_Text statusText;

    private TickerDTO[] tickers;
    private string selectedTicker;
    private string currentGameDate;
    private GamePhase currentPhase;
    private PortfolioTotalDTO[] cachedHoldings;
    private ChartTimeframe selectedTimeframe = ChartTimeframe.Month3;
    private bool showingPortfolioChart = false;

    void OnEnable()
    {
        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.OnStateChanged += OnPlayerStateChanged;
    }

    void OnDisable()
    {
        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.OnStateChanged -= OnPlayerStateChanged;
    }

    private void OnPlayerStateChanged(PlayerState oldState, PlayerState newState)
    {
        if (oldState == PlayerState.TRADING && tradingPanel.activeSelf)
            ClosePanel();
    }

    public void Open()
    {
        tradingPanel.SetActive(true);
        PlayerStateController.Inst.SetState(PlayerState.TRADING);

        closeButton.onClick.AddListener(Close);
        tickerDropdown.onValueChanged.AddListener(OnTickerChanged);
        BindTimeframeButtons();

        if (portfolioChartButton != null)
            portfolioChartButton.onClick.AddListener(ShowPortfolioChart);
        if (tickerChartButton != null)
            tickerChartButton.onClick.AddListener(ShowTickerChart);

        showingPortfolioChart = false;
        SetChartVisibility();

        _ = LoadInitialData();
    }

    public void Close()
    {
        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.MOVING);
    }

    private void ClosePanel()
    {
        closeButton.onClick.RemoveAllListeners();
        buyButton.onClick.RemoveAllListeners();
        sellButton.onClick.RemoveAllListeners();
        advanceDayButton.onClick.RemoveAllListeners();
        tickerDropdown.onValueChanged.RemoveAllListeners();
        UnbindTimeframeButtons();

        if (portfolioChartButton != null)
            portfolioChartButton.onClick.RemoveAllListeners();
        if (tickerChartButton != null)
            tickerChartButton.onClick.RemoveAllListeners();

        tradingPanel.SetActive(false);
    }

    // ── Data Loading ──

    private async Task LoadInitialData()
    {
        SetStatus("Loading...");
        try
        {
            await SyncPhase();
            await LoadTickers();
        }
        catch (System.Exception ex)
        {
            SetStatus($"Failed to load market data: {ex.Message}");
            return;
        }

        try
        {
            await RefreshPortfolio();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[TradingUI] Portfolio unavailable: {ex.Message}");
        }

        ApplyPhaseUI();
        SetStatus("Ready");
    }

    private async Task SyncPhase()
    {
        if (GamePhaseManager.Inst != null)
        {
            await GamePhaseManager.Inst.SyncWithServer();
            currentGameDate = GamePhaseManager.Inst.CurrentDate;
            currentPhase = GamePhaseManager.Inst.CurrentPhase;
        }
        else
        {
            var resp = await GameStateAPI.GetGameDate();
            currentGameDate = resp.current_date;
            currentPhase = GamePhase.PreMarket;
        }
        dateText.text = FormatDateHeader();
    }

    private async Task LoadTickers()
    {
        var resp = await MarketAPI.GetTickers();
        tickers = resp.tickers;

        tickerDropdown.ClearOptions();
        var options = new List<string>();
        if (tickers != null)
        {
            foreach (var t in tickers)
                options.Add(t.ticker_id);
        }
        tickerDropdown.AddOptions(options);

        if (tickers != null && tickers.Length > 0)
        {
            selectedTicker = tickers[0].ticker_id;
            UpdateCompanyName(0);
            await RefreshPrice();
        }
    }

    private void OnTickerChanged(int index)
    {
        if (tickers == null || index >= tickers.Length) return;
        selectedTicker = tickers[index].ticker_id;
        UpdateCompanyName(index);
        _ = RefreshPrice();
    }

    private void UpdateCompanyName(int index)
    {
        if (companyNameText == null) return;
        if (tickers != null && index < tickers.Length)
            companyNameText.text = tickers[index].company_name ?? "";
        else
            companyNameText.text = "";
    }

    // ── Phase-Aware UI ──

    private void ApplyPhaseUI()
    {
        buyButton.onClick.RemoveAllListeners();
        sellButton.onClick.RemoveAllListeners();
        advanceDayButton.onClick.RemoveAllListeners();

        switch (currentPhase)
        {
            case GamePhase.PreMarket:
                ApplyPreMarketUI();
                break;
            case GamePhase.Day:
                ApplyDayUI();
                break;
            case GamePhase.PostMarket:
                ApplyPostMarketUI();
                break;
        }
    }

    private void ApplyPreMarketUI()
    {
        buyButton.gameObject.SetActive(true);
        sellButton.gameObject.SetActive(true);
        quantityInput.gameObject.SetActive(true);
        tickerDropdown.gameObject.SetActive(true);
        advanceDayButton.interactable = true;

        buyButton.onClick.AddListener(OnQueueBuy);
        sellButton.onClick.AddListener(OnQueueSell);
        advanceDayButton.onClick.AddListener(OnOpenMarkets);
        SetAdvanceButtonText("Open Markets");

        RebuildHoldingsDisplay();
    }

    private void ApplyDayUI()
    {
        buyButton.gameObject.SetActive(false);
        sellButton.gameObject.SetActive(false);
        quantityInput.gameObject.SetActive(false);
        tickerDropdown.gameObject.SetActive(true);
        advanceDayButton.interactable = true;

        advanceDayButton.onClick.AddListener(OnCloseMarkets);
        SetAdvanceButtonText("Close Markets");

        RebuildHoldingsDisplay();
    }

    private void ApplyPostMarketUI()
    {
        buyButton.gameObject.SetActive(false);
        sellButton.gameObject.SetActive(false);
        quantityInput.gameObject.SetActive(false);
        tickerDropdown.gameObject.SetActive(true);
        advanceDayButton.interactable = true;

        advanceDayButton.onClick.AddListener(OnNextDay);
        SetAdvanceButtonText("Go to Sleep");

        RebuildHoldingsDisplay();
    }

    // ── Pre-Market: Queue Orders ──

    private void OnQueueBuy() => _ = QueueOrderAsync("buy");
    private void OnQueueSell() => _ = QueueOrderAsync("sell");

    private async Task QueueOrderAsync(string side)
    {
        if (GamePhaseManager.Inst == null) return;

        if (string.IsNullOrEmpty(selectedTicker))
        {
            SetStatus("Select a ticker first.");
            return;
        }
        if (!tickerTradableToday)
        {
            SetStatus("This ticker has no data today (halted/delisted).");
            return;
        }
        if (!int.TryParse(quantityInput.text, out int qty) || qty <= 0)
        {
            SetStatus("Enter a valid quantity.");
            return;
        }

        SetStatus($"Queueing {side}...");
        bool success = await GamePhaseManager.Inst.QueueOrder(selectedTicker, side, qty);
        if (success)
        {
            SetStatus($"Queued: {side.ToUpper()} {qty} {selectedTicker}");
            await RebuildHoldingsDisplayAsync();
        }
        else
        {
            string err = GamePhaseManager.Inst.LastOrderError ?? "Failed to queue order.";
            SetStatus(err);
        }
    }

    // ── Holdings Display (adapts per phase) ──

    private void RebuildHoldingsDisplay()
    {
        _ = RebuildHoldingsDisplayAsync();
    }

    private async Task RebuildHoldingsDisplayAsync()
    {
        if (holdingsText == null) return;

        var sb = new System.Text.StringBuilder();

        // Holdings
        if (cachedHoldings != null && cachedHoldings.Length > 0)
        {
            sb.AppendLine("<b>Holdings</b>");
            foreach (var t in cachedHoldings)
                sb.AppendLine($"  {t.ticker_id}: {t.shares_held:F0} shares");
        }
        else
        {
            sb.AppendLine("<b>Holdings</b>");
            sb.AppendLine("  <color=#888888>None</color>");
        }

        sb.AppendLine();

        // Orders / results (phase-dependent)
        switch (currentPhase)
        {
            case GamePhase.PreMarket:
                if (GamePhaseManager.Inst != null)
                {
                    var orders = await GamePhaseManager.Inst.GetPendingOrders();
                    sb.AppendLine("<b>Pending Orders</b>");
                    if (orders != null && orders.Length > 0)
                    {
                        foreach (var o in orders)
                            sb.AppendLine($"  {o.side.ToUpper()} {o.quantity} {o.ticker_id}");
                    }
                    else
                    {
                        sb.AppendLine("  <color=#888888>None</color>");
                    }
                }
                break;

            case GamePhase.Day:
                if (GamePhaseManager.Inst != null)
                {
                    var results = GamePhaseManager.Inst.TodayResults;
                    sb.AppendLine("<b>Today's Fills</b>");
                    if (results.Count > 0)
                    {
                        foreach (var r in results)
                        {
                            if (r.status == "ok")
                                sb.AppendLine($"  {r.side.ToUpper()} {r.quantity} {r.ticker} @ ${r.fillPrice:F2}");
                            else
                                sb.AppendLine($"  {r.side.ToUpper()} {r.quantity} {r.ticker} - FAILED");
                        }
                    }
                    else
                    {
                        sb.AppendLine("  <color=#888888>None</color>");
                    }
                }
                break;

            case GamePhase.PostMarket:
                if (GamePhaseManager.Inst != null)
                {
                    var results = GamePhaseManager.Inst.TodayResults;
                    sb.AppendLine("<b>Trade Results</b>");
                    if (results.Count > 0)
                    {
                        double totalPnl = 0;
                        foreach (var r in results)
                        {
                            if (r.status != "ok")
                            {
                                sb.AppendLine($"  {r.side.ToUpper()} {r.quantity} {r.ticker} - FAILED");
                                continue;
                            }
                            string pnlColor = r.pnl >= 0 ? "#26BF59" : "#D93838";
                            string pnlSign = r.pnl >= 0 ? "+" : "-";
                            string pnlStr = $"<color={pnlColor}>{pnlSign}${System.Math.Abs(r.pnl):F2}</color>";
                            sb.AppendLine($"  {r.side.ToUpper()} {r.quantity} {r.ticker}");
                            sb.AppendLine($"    Open: ${r.fillPrice:F2}  Close: ${r.closePrice:F2}  {pnlStr}");
                            totalPnl += r.pnl;
                        }
                        sb.AppendLine();
                        string totalColor = totalPnl >= 0 ? "#26BF59" : "#D93838";
                        string totalSign = totalPnl >= 0 ? "+" : "-";
                        sb.AppendLine($"<b>Day P&L: <color={totalColor}>{totalSign}${System.Math.Abs(totalPnl):F2}</color></b>");
                    }
                    else
                    {
                        sb.AppendLine("  <color=#888888>None</color>");
                    }
                }
                break;
        }

        holdingsText.text = sb.ToString().TrimEnd();
    }

    // ── Phase Transitions ──

    private async void OnOpenMarkets()
    {
        if (GamePhaseManager.Inst == null) return;

        SetStatus("Opening markets...");
        advanceDayButton.interactable = false;

        var results = await GamePhaseManager.Inst.OpenMarkets();
        if (results == null)
        {
            SetStatus("Cannot open markets right now.");
            advanceDayButton.interactable = true;
            return;
        }

        int filled = 0, failed = 0;
        foreach (var r in results)
        {
            if (r.status == "ok") filled++;
            else failed++;
        }

        string msg = filled > 0 ? $"{filled} order(s) filled at open." : "Markets open — no orders.";
        if (failed > 0) msg += $" {failed} order(s) failed.";
        SetStatus(msg);

        if (KnowledgeGraphManager.Inst != null)
            _ = KnowledgeGraphManager.Inst.CheckTriggersAsync();

        currentPhase = GamePhase.Day;
        dateText.text = FormatDateHeader();
        await RefreshPrice();
        await RefreshPortfolio();
        ApplyPhaseUI();

        Close();
    }

    private async void OnCloseMarkets()
    {
        if (GamePhaseManager.Inst == null) return;

        SetStatus("Closing markets...");
        advanceDayButton.interactable = false;

        await GamePhaseManager.Inst.CloseMarkets();

        currentPhase = GamePhase.PostMarket;
        dateText.text = FormatDateHeader();
        await RefreshPrice();
        await RefreshPortfolio();
        ApplyPhaseUI();

        SetStatus("Markets closed. Review your results.");
    }

    private async void OnNextDay()
    {
        if (GamePhaseManager.Inst == null) return;

        SetStatus("Advancing to next day...");
        advanceDayButton.interactable = false;

        bool success = await GamePhaseManager.Inst.AdvanceToNextDay();
        if (!success)
        {
            SetStatus("Game over — no more trading days.");
            return;
        }

        currentGameDate = GamePhaseManager.Inst.CurrentDate;
        currentPhase = GamePhase.PreMarket;
        dateText.text = FormatDateHeader();

        await RefreshPrice();
        await RefreshPortfolio();
        ApplyPhaseUI();

        if (KnowledgeGraphManager.Inst != null)
            _ = KnowledgeGraphManager.Inst.CheckTriggersAsync();

        var liquidations = GamePhaseManager.Inst.LastForcedLiquidations;
        if (liquidations != null && liquidations.Length > 0)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("<color=#FF8800>DELISTED: </color>");
            foreach (var liq in liquidations)
                sb.Append($"{liq.ticker_id} ({liq.shares:F0} shares @ ${liq.price:F2})  ");
            SetStatus(sb.ToString().TrimEnd());
        }
        else
        {
            SetStatus($"New day: {currentGameDate}");
        }
    }

    // ── Chart Tab Switching ──

    private void ShowPortfolioChart()
    {
        if (showingPortfolioChart) return;
        showingPortfolioChart = true;
        SetChartVisibility();
        _ = LoadPortfolioChart();
    }

    private void ShowTickerChart()
    {
        if (!showingPortfolioChart) return;
        showingPortfolioChart = false;
        SetChartVisibility();
        _ = RefreshPrice();
    }

    private void SetChartVisibility()
    {
        if (ohlcChart != null)
            ohlcChart.gameObject.SetActive(!showingPortfolioChart);
        if (portfolioChart != null)
            portfolioChart.gameObject.SetActive(showingPortfolioChart);

        SetTabHighlight(tickerChartButton, !showingPortfolioChart);
        SetTabHighlight(portfolioChartButton, showingPortfolioChart);
    }

    private void SetTabHighlight(Button btn, bool active)
    {
        if (btn == null) return;
        var tmp = btn.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.color = active ? chartTabActiveColor : chartTabInactiveColor;
    }

    private async Task LoadPortfolioChart()
    {
        if (portfolioChart == null) return;

        try
        {
            var resp = await OrderAPI.GetPortfolioHistory(APIBootstrapper.EntityDbId);
            if (resp.history != null && resp.history.Length >= 2)
                portfolioChart.SetData(resp.history);
            else
                portfolioChart.Clear();
        }
        catch
        {
            portfolioChart.Clear();
        }
    }

    // ── Timeframe Selection ──

    private void BindTimeframeButtons()
    {
        if (btn1W != null) btn1W.onClick.AddListener(() => SetTimeframe(ChartTimeframe.Week1));
        if (btn1M != null) btn1M.onClick.AddListener(() => SetTimeframe(ChartTimeframe.Month1));
        if (btn3M != null) btn3M.onClick.AddListener(() => SetTimeframe(ChartTimeframe.Month3));
        if (btn1Y != null) btn1Y.onClick.AddListener(() => SetTimeframe(ChartTimeframe.Year1));
        if (btn5Y != null) btn5Y.onClick.AddListener(() => SetTimeframe(ChartTimeframe.Year5));
        UpdateTimeframeHighlight();
    }

    private void UnbindTimeframeButtons()
    {
        if (btn1W != null) btn1W.onClick.RemoveAllListeners();
        if (btn1M != null) btn1M.onClick.RemoveAllListeners();
        if (btn3M != null) btn3M.onClick.RemoveAllListeners();
        if (btn1Y != null) btn1Y.onClick.RemoveAllListeners();
        if (btn5Y != null) btn5Y.onClick.RemoveAllListeners();
    }

    private void SetTimeframe(ChartTimeframe tf)
    {
        selectedTimeframe = tf;
        UpdateTimeframeHighlight();
        _ = RefreshPrice();
    }

    private void UpdateTimeframeHighlight()
    {
        SetButtonColor(btn1W, selectedTimeframe == ChartTimeframe.Week1);
        SetButtonColor(btn1M, selectedTimeframe == ChartTimeframe.Month1);
        SetButtonColor(btn3M, selectedTimeframe == ChartTimeframe.Month3);
        SetButtonColor(btn1Y, selectedTimeframe == ChartTimeframe.Year1);
        SetButtonColor(btn5Y, selectedTimeframe == ChartTimeframe.Year5);
    }

    private void SetButtonColor(Button btn, bool active)
    {
        if (btn == null) return;
        var tmp = btn.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.color = active ? timeframeActiveColor : timeframeInactiveColor;
    }

    // ── Refresh Helpers ──

    private const int MinChartDataPoints = 15;

    private bool tickerTradableToday = true;

    private async Task RefreshPrice()
    {
        if (string.IsNullOrEmpty(selectedTicker) || string.IsNullOrEmpty(currentGameDate))
        {
            priceText.text = "---";
            if (ohlcChart != null) ohlcChart.Clear();
            SetTickerTradable(true);
            return;
        }

        try
        {
            string chartEndDate = currentGameDate;
            if (currentPhase == GamePhase.Day)
            {
                if (System.DateTime.TryParse(currentGameDate, out var dt))
                    chartEndDate = dt.AddDays(-1).ToString("yyyy-MM-dd");
            }

            // Fetch chart data with selected timeframe
            int lookback = CandleAggregator.LookbackCalendarDays(selectedTimeframe);
            string startDate = null;
            if (System.DateTime.TryParse(chartEndDate, out var endDt))
                startDate = endDt.AddDays(-lookback).ToString("yyyy-MM-dd");

            var resp = await MarketAPI.GetPrices(selectedTicker, startDate, chartEndDate);

            // If too few data points, fetch all available history for this ticker
            if (resp.rows == null || resp.rows.Length < MinChartDataPoints)
            {
                var allResp = await MarketAPI.GetPrices(selectedTicker, null, chartEndDate);
                if (allResp.rows != null && allResp.rows.Length > resp.rows?.Length)
                    resp = allResp;
            }

            if (resp.rows != null && resp.rows.Length > 0)
            {
                var chartData = CandleAggregator.Aggregate(resp.rows, selectedTimeframe);
                if (ohlcChart != null && !showingPortfolioChart) ohlcChart.SetData(chartData);

                var prevClose = resp.rows[resp.rows.Length - 1];

                switch (currentPhase)
                {
                    case GamePhase.PreMarket:
                        var todayResp = await MarketAPI.GetPrices(selectedTicker, currentGameDate, currentGameDate);
                        if (todayResp.rows != null && todayResp.rows.Length > 0)
                        {
                            priceText.text = $"${prevClose.close_price:F2}";
                            SetTickerTradable(true);
                        }
                        else
                        {
                            priceText.text = $"${prevClose.close_price:F2}   <color=#FF8800>(No data today — halted/delisted)</color>";
                            SetTickerTradable(false);
                        }
                        break;

                    case GamePhase.Day:
                        priceText.text = $"${prevClose.close_price:F2}";
                        SetTickerTradable(true);
                        break;

                    case GamePhase.PostMarket:
                        var pmResp = await MarketAPI.GetPrices(selectedTicker, currentGameDate, currentGameDate);
                        if (pmResp.rows != null && pmResp.rows.Length > 0)
                        {
                            var today = pmResp.rows[0];
                            double change = today.close_price - prevClose.close_price;
                            double changePct = prevClose.close_price > 0
                                ? change / prevClose.close_price * 100.0 : 0;
                            string sign = change >= 0 ? "+" : "";
                            string color = change >= 0 ? "#26BF59" : "#D93838";
                            priceText.text = $"${today.close_price:F2}   <color={color}>{sign}{change:F2} ({sign}{changePct:F1}%)</color>";

                            if (ohlcChart != null && !showingPortfolioChart)
                            {
                                var fullResp = await MarketAPI.GetPrices(selectedTicker, startDate, currentGameDate);
                                if (fullResp.rows == null || fullResp.rows.Length < MinChartDataPoints)
                                {
                                    var allFull = await MarketAPI.GetPrices(selectedTicker, null, currentGameDate);
                                    if (allFull.rows != null && allFull.rows.Length > (fullResp.rows?.Length ?? 0))
                                        fullResp = allFull;
                                }
                                if (fullResp.rows != null && fullResp.rows.Length > 0)
                                {
                                    var fullData = CandleAggregator.Aggregate(fullResp.rows, selectedTimeframe);
                                    ohlcChart.SetData(fullData);
                                }
                            }
                        }
                        else
                        {
                            priceText.text = $"${prevClose.close_price:F2}   <color=#FF8800>(No data today)</color>";
                        }
                        SetTickerTradable(true);
                        break;
                }
            }
            else
            {
                if (ohlcChart != null) ohlcChart.Clear();
                priceText.text = "<color=#FF8800>No price data available for this ticker</color>";
                SetTickerTradable(false);
            }
        }
        catch
        {
            priceText.text = "Failed to load price";
            SetTickerTradable(false);
        }
    }

    private void SetTickerTradable(bool tradable)
    {
        tickerTradableToday = tradable;
        if (currentPhase == GamePhase.PreMarket)
        {
            buyButton.interactable = tradable;
            sellButton.interactable = tradable;
        }
    }

    private async Task RefreshPortfolio()
    {
        try
        {
            var resp = await TradeAPI.GetPortfolio(APIBootstrapper.EntityDbId);
            double cash = resp.entity.available_cash;
            cashText.text = $"Cash: ${cash:N2}";
            cachedHoldings = resp.totals;

            if (netWorthText != null)
            {
                double holdingsValue = 0;
                if (cachedHoldings != null && !string.IsNullOrEmpty(currentGameDate))
                {
                    foreach (var h in cachedHoldings)
                    {
                        try
                        {
                            var priceResp = await MarketAPI.GetPrices(h.ticker_id, currentGameDate, currentGameDate);
                            if (priceResp.rows != null && priceResp.rows.Length > 0)
                                holdingsValue += h.shares_held * priceResp.rows[0].close_price;
                        }
                        catch { }
                    }
                }
                double netWorth = cash + holdingsValue;
                string color = netWorth >= 10000 ? "#26BF59" : "#D93838";
                netWorthText.text = $"Net Worth: <color={color}>${netWorth:N2}</color>";
            }
        }
        catch
        {
            cashText.text = "Cash: ---";
            if (netWorthText != null) netWorthText.text = "Net Worth: ---";
            cachedHoldings = null;
        }
    }

    // ── UI Helpers ──

    private void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
    }

    private void SetAdvanceButtonText(string text)
    {
        if (advanceButtonText != null)
        {
            advanceButtonText.text = text;
            return;
        }
        var tmp = advanceDayButton.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.text = text;
    }

    private string FormatDateHeader()
    {
        string phaseLabel = currentPhase switch
        {
            GamePhase.PreMarket => "PRE-MARKET",
            GamePhase.Day => "MARKETS OPEN",
            GamePhase.PostMarket => "POST-MARKET",
            _ => ""
        };

        string timeStr = "";
        if (currentPhase == GamePhase.Day && GamePhaseManager.Inst != null && GamePhaseManager.Inst.DayTimerActive)
        {
            float t = GamePhaseManager.Inst.DayTimeRemaining;
            int min = Mathf.FloorToInt(t / 60f);
            int sec = Mathf.FloorToInt(t % 60f);
            timeStr = $" | {min}:{sec:D2}";
        }

        return $"{currentGameDate ?? "---"} | {phaseLabel}{timeStr}";
    }
}
