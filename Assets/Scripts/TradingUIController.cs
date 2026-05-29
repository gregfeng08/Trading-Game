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

    [Header("Chart Tabs")]
    [SerializeField] private Button tickerChartButton;
    [SerializeField] private Button portfolioChartButton;
    [SerializeField] private PortfolioLineChart portfolioChart;
    [SerializeField] private Color chartTabActiveBg = new Color(0.2f, 0.6f, 0.9f, 1f);
    [SerializeField] private Color chartTabInactiveBg = new Color(0.22f, 0.23f, 0.28f, 1f);
    [SerializeField] private Color chartTabActiveText = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color chartTabInactiveText = new Color(0.65f, 0.65f, 0.7f, 1f);

    [Header("Chart Timeframe")]
    [SerializeField] private Button btn1W;
    [SerializeField] private Button btn1M;
    [SerializeField] private Button btn3M;
    [SerializeField] private Button btn1Y;
    [SerializeField] private Button btn5Y;
    [SerializeField] private Color timeframeActiveBg = new Color(0.2f, 0.6f, 0.9f, 1f);
    [SerializeField] private Color timeframeInactiveBg = new Color(0.22f, 0.23f, 0.28f, 1f);
    [SerializeField] private Color timeframeActiveText = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color timeframeInactiveText = new Color(0.65f, 0.65f, 0.7f, 1f);

    [Header("Bottom Tabs")]
    [SerializeField] private Button tradeTabButton;
    [SerializeField] private Button portfolioTabButton;
    [SerializeField] private Button historyTabButton;
    [SerializeField] private GameObject tradeTabContent;
    [SerializeField] private GameObject portfolioTabContent;
    [SerializeField] private GameObject historyTabContent;
    [SerializeField] private Color tabActiveColor = new Color(0.2f, 0.6f, 0.9f, 1f);
    [SerializeField] private Color tabInactiveColor = new Color(0.22f, 0.23f, 0.28f, 1f);
    [SerializeField] private Color tabActiveTextColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color tabInactiveTextColor = new Color(0.65f, 0.65f, 0.7f, 1f);

    [Header("Trade Tab")]
    [SerializeField] private TMP_InputField quantityInput;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private Button advanceDayButton;
    [SerializeField] private TMP_Text advanceButtonText;
    [SerializeField] private TMP_Text ordersText;

    [Header("Portfolio Tab")]
    [SerializeField] private TMP_Text holdingsText;

    [Header("History Tab")]
    [SerializeField] private TMP_Text historyText;

    [Header("Always Visible")]
    [SerializeField] private TMP_Text cashText;
    [SerializeField] private TMP_Text netWorthText;
    [SerializeField] private TMP_Text statusText;

    private enum BottomTab { Trade, Portfolio, History }

    private TickerDTO[] tickers;
    private string selectedTicker;
    private string currentGameDate;
    private GamePhase currentPhase;
    private PortfolioTotalDTO[] cachedHoldings;
    private PortfolioLotDTO[] cachedLots;
    private ChartTimeframe selectedTimeframe = ChartTimeframe.Month3;
    private bool showingPortfolioChart = false;
    private BottomTab activeTab = BottomTab.Trade;
    private bool tickerTradableToday = true;
    private double currentEstimatedPrice;
    private NetWorthPointDTO[] cachedPortfolioHistory;

    private GameObject dismissBackground;

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

    private bool hasOpenedBefore;

    public void Open()
    {
        ShowDismissBackground();
        tradingPanel.SetActive(true);
        PlayerStateController.Inst.SetState(PlayerState.TRADING);

        closeButton.onClick.AddListener(Close);
        tickerDropdown.onValueChanged.AddListener(OnTickerChanged);
        BindTimeframeButtons();
        BindChartTabs();
        BindBottomTabs();

        if (!hasOpenedBefore)
        {
            showingPortfolioChart = false;
            activeTab = BottomTab.Trade;
            hasOpenedBefore = true;
        }

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
        if (advanceDayButton != null) advanceDayButton.onClick.RemoveAllListeners();
        tickerDropdown.onValueChanged.RemoveAllListeners();
        UnbindTimeframeButtons();
        UnbindChartTabs();
        UnbindBottomTabs();

        HideDismissBackground();
        tradingPanel.SetActive(false);
    }

    private void ShowDismissBackground()
    {
        if (dismissBackground == null)
        {
            var parent = tradingPanel.transform.parent;
            if (parent == null) return;

            dismissBackground = new GameObject("DismissBackground");
            dismissBackground.transform.SetParent(parent, false);
            var rt = dismissBackground.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            var img = dismissBackground.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.4f);

            var btn = dismissBackground.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Close);
        }

        dismissBackground.SetActive(true);
        int panelIdx = tradingPanel.transform.GetSiblingIndex();
        dismissBackground.transform.SetSiblingIndex(panelIdx);
    }

    private void HideDismissBackground()
    {
        if (dismissBackground != null)
            dismissBackground.SetActive(false);
    }

    // ── Bottom Tab System ──

    private void BindBottomTabs()
    {
        if (tradeTabButton != null)
            tradeTabButton.onClick.AddListener(() => SwitchTab(BottomTab.Trade));
        if (portfolioTabButton != null)
            portfolioTabButton.onClick.AddListener(() => SwitchTab(BottomTab.Portfolio));
        if (historyTabButton != null)
            historyTabButton.onClick.AddListener(() => SwitchTab(BottomTab.History));
    }

    private void UnbindBottomTabs()
    {
        if (tradeTabButton != null) tradeTabButton.onClick.RemoveAllListeners();
        if (portfolioTabButton != null) portfolioTabButton.onClick.RemoveAllListeners();
        if (historyTabButton != null) historyTabButton.onClick.RemoveAllListeners();
    }

    private void SwitchTab(BottomTab tab)
    {
        activeTab = tab;

        if (tradeTabContent != null) tradeTabContent.SetActive(tab == BottomTab.Trade);
        if (portfolioTabContent != null) portfolioTabContent.SetActive(tab == BottomTab.Portfolio);
        if (historyTabContent != null) historyTabContent.SetActive(tab == BottomTab.History);

        HighlightTabButton(tradeTabButton, tab == BottomTab.Trade);
        HighlightTabButton(portfolioTabButton, tab == BottomTab.Portfolio);
        HighlightTabButton(historyTabButton, tab == BottomTab.History);

        switch (tab)
        {
            case BottomTab.Trade:
                RefreshOrdersDisplay();
                break;
            case BottomTab.Portfolio:
                RefreshHoldingsDisplay();
                break;
            case BottomTab.History:
                _ = RefreshHistoryDisplay();
                break;
        }
    }

    private void HighlightTabButton(Button btn, bool active)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = active ? tabActiveColor : tabInactiveColor;
        var tmp = btn.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.color = active ? tabActiveTextColor : tabInactiveTextColor;
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
        SwitchTab(activeTab);
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
            int restoreIndex = 0;
            if (!string.IsNullOrEmpty(selectedTicker))
            {
                for (int i = 0; i < tickers.Length; i++)
                {
                    if (tickers[i].ticker_id == selectedTicker) { restoreIndex = i; break; }
                }
            }

            tickerDropdown.SetValueWithoutNotify(restoreIndex);
            selectedTicker = tickers[restoreIndex].ticker_id;
            UpdateCompanyName(restoreIndex);
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
        if (advanceDayButton != null) advanceDayButton.onClick.RemoveAllListeners();

        switch (currentPhase)
        {
            case GamePhase.PreMarket:
                buyButton.gameObject.SetActive(true);
                sellButton.gameObject.SetActive(true);
                quantityInput.gameObject.SetActive(true);
                if (advanceDayButton != null)
                {
                    advanceDayButton.gameObject.SetActive(true);
                    advanceDayButton.interactable = true;
                    advanceDayButton.onClick.AddListener(OnOpenMarkets);
                    SetAdvanceButtonText("Confirm Trades");
                }

                buyButton.onClick.AddListener(OnQueueBuy);
                sellButton.onClick.AddListener(OnQueueSell);
                break;

            case GamePhase.Day:
            case GamePhase.PostMarket:
                buyButton.gameObject.SetActive(false);
                sellButton.gameObject.SetActive(false);
                quantityInput.gameObject.SetActive(false);
                if (advanceDayButton != null)
                    advanceDayButton.gameObject.SetActive(false);
                break;
        }
    }

    // ── Trade Tab: Orders Display ──

    private void RefreshOrdersDisplay()
    {
        _ = RefreshOrdersDisplayAsync();
    }

    private async Task RefreshOrdersDisplayAsync()
    {
        if (ordersText == null) return;

        var sb = new System.Text.StringBuilder();

        switch (currentPhase)
        {
            case GamePhase.PreMarket:
                if (GamePhaseManager.Inst != null)
                {
                    var orders = GamePhaseManager.Inst.PendingOrders;
                    if (orders.Count > 0)
                    {
                        foreach (var o in orders)
                        {
                            string sideColor = o.side == "buy" ? "#26BF59" : "#D93838";
                            sb.AppendLine($"<color={sideColor}>{o.side.ToUpper()}</color>  {o.quantity} {o.ticker}  ~${FmtPrice(o.estimatedPrice)}");
                        }
                    }
                    else
                    {
                        sb.AppendLine("<color=#888888>No pending orders.</color>");
                        sb.AppendLine("<color=#888888>Queue orders above, then Confirm Trades.</color>");
                    }
                }
                break;

            case GamePhase.Day:
                if (GamePhaseManager.Inst != null)
                {
                    var results = GamePhaseManager.Inst.TodayResults;
                    if (results.Count > 0)
                    {
                        foreach (var r in results)
                        {
                            string sideColor = r.side == "buy" ? "#26BF59" : "#D93838";
                            if (r.status == "ok")
                                sb.AppendLine($"<color={sideColor}>{r.side.ToUpper()}</color>  {r.quantity} {r.ticker} @ ${FmtPrice(r.fillPrice)}");
                            else
                                sb.AppendLine($"<color={sideColor}>{r.side.ToUpper()}</color>  {r.quantity} {r.ticker} — <color=#D93838>FAILED</color>");
                        }
                    }
                    else
                    {
                        sb.AppendLine("<color=#888888>No trades today.</color>");
                    }
                }
                break;

            case GamePhase.PostMarket:
                if (GamePhaseManager.Inst != null)
                {
                    var results = GamePhaseManager.Inst.TodayResults;
                    if (results.Count > 0)
                    {
                        double totalPnl = 0;
                        foreach (var r in results)
                        {
                            if (r.status != "ok")
                            {
                                sb.AppendLine($"{r.side.ToUpper()} {r.quantity} {r.ticker} — <color=#D93838>FAILED</color>");
                                continue;
                            }
                            string sideColor = r.side == "buy" ? "#26BF59" : "#D93838";
                            string pnlColor = r.pnl >= 0 ? "#26BF59" : "#D93838";
                            string pnlSign = r.pnl >= 0 ? "+" : "";
                            sb.AppendLine($"<color={sideColor}>{r.side.ToUpper()}</color>  {r.quantity} {r.ticker}");
                            sb.AppendLine($"  Open ${FmtPrice(r.fillPrice)}  →  Close ${FmtPrice(r.closePrice)}  <color={pnlColor}>{pnlSign}${FmtPrice(r.pnl)}</color>");
                            totalPnl += r.pnl;
                        }
                        sb.AppendLine();
                        string totalColor = totalPnl >= 0 ? "#26BF59" : "#D93838";
                        string totalSign = totalPnl >= 0 ? "+" : "";
                        sb.AppendLine($"<b>Day P&L:  <color={totalColor}>{totalSign}${FmtPrice(totalPnl)}</color></b>");
                    }
                    else
                    {
                        sb.AppendLine("<color=#888888>No trades today.</color>");
                    }
                }
                break;
        }

        ordersText.text = sb.ToString().TrimEnd();
    }

    // ── Portfolio Tab: Holdings Display ──

    private void RefreshHoldingsDisplay()
    {
        if (holdingsText == null) return;

        var sb = new System.Text.StringBuilder();

        if (cachedHoldings != null && cachedHoldings.Length > 0)
        {
            foreach (var h in cachedHoldings)
            {
                sb.AppendLine($"<b>{h.ticker_id}</b>");
                sb.Append($"  {h.shares_held:F0} shares");

                if (cachedLots != null)
                {
                    double totalCost = 0, totalShares = 0;
                    foreach (var lot in cachedLots)
                    {
                        if (lot.ticker_id == h.ticker_id)
                        {
                            totalCost += lot.shares_held * lot.price;
                            totalShares += lot.shares_held;
                        }
                    }
                    if (totalShares > 0)
                    {
                        double avg = totalCost / totalShares;
                        sb.Append($"  ·  Avg ${FmtPrice(avg)}");
                    }
                }
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("<color=#888888>No holdings yet.</color>");
            sb.AppendLine("<color=#888888>Place a trade to get started!</color>");
        }

        holdingsText.text = sb.ToString().TrimEnd();
    }

    // ── History Tab ──

    private async Task RefreshHistoryDisplay()
    {
        if (historyText == null) return;

        historyText.text = "<color=#888888>Loading...</color>";

        try
        {
            var resp = await TradeAPI.GetTradeHistory(APIBootstrapper.EntityDbId);
            if (resp.rows == null || resp.rows.Length == 0)
            {
                historyText.text = "<color=#888888>No trade history yet.</color>";
                return;
            }

            var sb = new System.Text.StringBuilder();
            string lastDate = null;

            for (int i = resp.rows.Length - 1; i >= 0; i--)
            {
                var row = resp.rows[i];
                if (row.trade_date != lastDate)
                {
                    if (lastDate != null) sb.AppendLine();
                    sb.AppendLine($"<b>{row.trade_date}</b>");
                    lastDate = row.trade_date;
                }

                string side = row.shares >= 0 ? "BUY" : "SELL";
                string sideColor = row.shares >= 0 ? "#26BF59" : "#D93838";
                double absShares = System.Math.Abs(row.shares);
                sb.AppendLine($"  <color={sideColor}>{side}</color>  {absShares:F0} {row.ticker_id} @ ${FmtPrice(row.price_paid)}");
            }

            historyText.text = sb.ToString().TrimEnd();
        }
        catch
        {
            historyText.text = "<color=#D93838>Failed to load history.</color>";
        }
    }

    // ── Pre-Market: Queue Orders ──

    private void OnQueueBuy() => QueueOrder("buy");
    private void OnQueueSell() => QueueOrder("sell");

    private void QueueOrder(string side)
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

        double heldShares = 0;
        if (cachedHoldings != null)
            foreach (var h in cachedHoldings)
                if (h.ticker_id == selectedTicker) { heldShares = h.shares_held; break; }

        bool success = GamePhaseManager.Inst.QueueOrder(selectedTicker, side, qty, currentEstimatedPrice, heldShares);
        if (success)
        {
            SetStatus($"Queued: {side.ToUpper()} {qty} {selectedTicker}");
            RefreshCashDisplay();
            if (activeTab == BottomTab.Trade)
                RefreshOrdersDisplay();
        }
        else
        {
            SetStatus(GamePhaseManager.Inst.LastOrderError ?? "Failed to queue order.");
        }
    }

    // ── Phase Transitions ──

    private async void OnOpenMarkets()
    {
        if (GamePhaseManager.Inst == null) return;

        SetStatus("Confirming trades...");
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

        string msg = filled > 0 ? $"{filled} order(s) filled." : "Markets open — no orders.";
        if (failed > 0) msg += $" {failed} failed.";
        SetStatus(msg);

        currentPhase = GamePhase.Day;
        dateText.text = FormatDateHeader();
        await RefreshPrice();
        await RefreshPortfolio();
        if (showingPortfolioChart)
            await LoadPortfolioChart();
        ApplyPhaseUI();
        SwitchTab(activeTab);

        Close();
    }

    // ── Chart Tab Switching ──

    private void BindChartTabs()
    {
        if (portfolioChartButton != null)
            portfolioChartButton.onClick.AddListener(ShowPortfolioChart);
        if (tickerChartButton != null)
            tickerChartButton.onClick.AddListener(ShowTickerChart);
    }

    private void UnbindChartTabs()
    {
        if (portfolioChartButton != null) portfolioChartButton.onClick.RemoveAllListeners();
        if (tickerChartButton != null) tickerChartButton.onClick.RemoveAllListeners();
    }

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

        SetChartTabHighlight(tickerChartButton, !showingPortfolioChart);
        SetChartTabHighlight(portfolioChartButton, showingPortfolioChart);
    }

    private void SetChartTabHighlight(Button btn, bool active)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = active ? chartTabActiveBg : chartTabInactiveBg;
        var tmp = btn.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.color = active ? chartTabActiveText : chartTabInactiveText;
    }

    private async Task LoadPortfolioChart()
    {
        if (portfolioChart == null) return;

        try
        {
            var resp = await OrderAPI.GetPortfolioHistory(APIBootstrapper.EntityDbId);
            cachedPortfolioHistory = resp.history;
            ApplyPortfolioTimeframe();
        }
        catch
        {
            cachedPortfolioHistory = null;
            portfolioChart.Clear();
        }
    }

    private void ApplyPortfolioTimeframe()
    {
        if (portfolioChart == null) return;
        if (cachedPortfolioHistory == null || cachedPortfolioHistory.Length < 2)
        {
            portfolioChart.Clear();
            return;
        }

        int lookback = CandleAggregator.LookbackCalendarDays(selectedTimeframe);
        string refDate = currentGameDate ?? cachedPortfolioHistory[cachedPortfolioHistory.Length - 1].date;

        if (System.DateTime.TryParse(refDate, out var end))
        {
            var cutoff = end.AddDays(-lookback);
            var filtered = System.Array.FindAll(cachedPortfolioHistory,
                p => System.DateTime.TryParse(p.date, out var d) && d >= cutoff);

            int max = CandleAggregator.MaxBars(selectedTimeframe);
            if (filtered.Length > max)
            {
                var trimmed = new NetWorthPointDTO[max];
                System.Array.Copy(filtered, filtered.Length - max, trimmed, 0, max);
                filtered = trimmed;
            }

            if (filtered.Length >= 2)
                portfolioChart.SetData(filtered);
            else
                portfolioChart.SetData(cachedPortfolioHistory);
        }
        else
        {
            portfolioChart.SetData(cachedPortfolioHistory);
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

        if (showingPortfolioChart)
            ApplyPortfolioTimeframe();
        else
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
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = active ? timeframeActiveBg : timeframeInactiveBg;
        var tmp = btn.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.color = active ? timeframeActiveText : timeframeInactiveText;
    }

    // ── Refresh Helpers ──

    private const int MinChartDataPoints = 2;

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

            int lookback = CandleAggregator.LookbackCalendarDays(selectedTimeframe);
            string startDate = null;
            if (System.DateTime.TryParse(chartEndDate, out var endDt))
                startDate = endDt.AddDays(-lookback).ToString("yyyy-MM-dd");

            var resp = await MarketAPI.GetPrices(selectedTicker, startDate, chartEndDate);

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

                currentEstimatedPrice = prevClose.close_price;

                switch (currentPhase)
                {
                    case GamePhase.PreMarket:
                        var todayResp = await MarketAPI.GetPrices(selectedTicker, currentGameDate, currentGameDate);
                        if (todayResp.rows != null && todayResp.rows.Length > 0)
                        {
                            double openPrice = todayResp.rows[0].open_price;
                            currentEstimatedPrice = openPrice;
                            priceText.text = $"${FmtPrice(openPrice)}";
                            SetTickerTradable(true);
                        }
                        else
                        {
                            priceText.text = $"${FmtPrice(prevClose.close_price)}   <color=#FF8800>(halted/delisted)</color>";
                            SetTickerTradable(false);
                        }
                        break;

                    case GamePhase.Day:
                        priceText.text = $"${FmtPrice(prevClose.close_price)}";
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
                            priceText.text = $"${FmtPrice(today.close_price)}   <color={color}>{sign}{FmtPrice(change)} ({sign}{changePct:F1}%)</color>";

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
                            priceText.text = $"${FmtPrice(prevClose.close_price)}   <color=#FF8800>(No data today)</color>";
                        }
                        SetTickerTradable(true);
                        break;
                }

                AppendPeriodChange(chartData);
            }
            else
            {
                if (ohlcChart != null) ohlcChart.Clear();
                priceText.text = "<color=#FF8800>No price data available</color>";
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
            cachedHoldings = resp.totals;
            cachedLots = resp.lots;

            if (GamePhaseManager.Inst != null)
                GamePhaseManager.Inst.SetServerCash(cash);

            RefreshCashDisplay();

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
                double effectiveCash = GamePhaseManager.Inst != null
                    ? GamePhaseManager.Inst.EffectiveAvailableCash : cash;
                double netWorth = effectiveCash + holdingsValue;
                string color = netWorth >= 10000 ? "#26BF59" : "#D93838";
                netWorthText.text = $"Net Worth: <color={color}>${netWorth:N2}</color>";
            }
        }
        catch
        {
            cashText.text = "Cash: ---";
            if (netWorthText != null) netWorthText.text = "Net Worth: ---";
            cachedHoldings = null;
            cachedLots = null;
        }
    }

    private void RefreshCashDisplay()
    {
        if (cashText == null) return;
        if (GamePhaseManager.Inst != null)
        {
            double effective = GamePhaseManager.Inst.EffectiveAvailableCash;
            cashText.text = $"Cash: ${effective:N2}";
        }
    }

    // ── UI Helpers ──

    private void AppendPeriodChange(PriceRowDTO[] rows)
    {
        if (rows == null || rows.Length < 2 || priceText == null) return;

        double periodOpen = rows[0].open_price;
        double periodClose = rows[rows.Length - 1].close_price;
        if (periodOpen <= 0) return;

        double pctChange = (periodClose - periodOpen) / periodOpen * 100.0;
        string sign = pctChange >= 0 ? "+" : "";
        string color = pctChange >= 0 ? "#26BF59" : "#D93838";
        string label = TimeframeLabel(selectedTimeframe);

        priceText.text += $"   <size=80%><color={color}>{sign}{pctChange:F1}% {label}</color></size>";
    }

    private static string TimeframeLabel(ChartTimeframe tf)
    {
        return tf switch
        {
            ChartTimeframe.Week1 => "1W",
            ChartTimeframe.Month1 => "1M",
            ChartTimeframe.Month3 => "3M",
            ChartTimeframe.Year1 => "1Y",
            ChartTimeframe.Year5 => "5Y",
            _ => ""
        };
    }

    public static string FmtPrice(double price)
    {
        double abs = System.Math.Abs(price);
        if (abs < 1.0) return price.ToString("F4");
        if (abs >= 1000.0) return price.ToString("F0");
        return price.ToString("F2");
    }

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
        if (advanceDayButton != null)
        {
            var tmp = advanceDayButton.GetComponentInChildren<TMP_Text>();
            if (tmp != null) tmp.text = text;
        }
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
