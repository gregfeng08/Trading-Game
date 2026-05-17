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
    [SerializeField] private OHLCChart ohlcChart;
    [SerializeField] private int chartLookbackDays = 120;

    [Header("Trade Controls")]
    [SerializeField] private TMP_InputField quantityInput;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private Button advanceDayButton;
    [SerializeField] private TMP_Text advanceButtonText;

    [Header("Player Info")]
    [SerializeField] private TMP_Text cashText;
    [SerializeField] private TMP_Text holdingsText;

    [Header("Feedback")]
    [SerializeField] private TMP_Text statusText;

    private TickerDTO[] tickers;
    private string selectedTicker;
    private string currentGameDate;
    private GamePhase currentPhase;
    private PortfolioTotalDTO[] cachedHoldings;

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

        tradingPanel.SetActive(false);
    }

    // ── Data Loading ──

    private async Task EnsureEntityDbId()
    {
        if (APIBootstrapper.EntityDbId >= 0) return;
        var resp = await TradeAPI.ResolveEntity(APIBootstrapper.EntityExternalId);
        APIBootstrapper.EntityDbId = resp.entity_db_id;
    }

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
            await EnsureEntityDbId();
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
                options.Add($"{t.ticker_id} - {t.company_name}");
        }
        tickerDropdown.AddOptions(options);

        if (tickers != null && tickers.Length > 0)
        {
            selectedTicker = tickers[0].ticker_id;
            await RefreshPrice();
        }
    }

    private void OnTickerChanged(int index)
    {
        if (tickers == null || index >= tickers.Length) return;
        selectedTicker = tickers[index].ticker_id;
        _ = RefreshPrice();
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
        if (!int.TryParse(quantityInput.text, out int qty) || qty <= 0)
        {
            SetStatus("Enter a valid quantity.");
            return;
        }

        GamePhaseManager.Inst.QueueOrder(selectedTicker, side, qty);
        SetStatus($"Queued: {side.ToUpper()} {qty} {selectedTicker}");
        RebuildHoldingsDisplay();
    }

    // ── Holdings Display (adapts per phase) ──

    private void RebuildHoldingsDisplay()
    {
        var sb = new System.Text.StringBuilder();

        switch (currentPhase)
        {
            case GamePhase.PreMarket:
                BuildPendingOrdersSection(sb);
                break;
            case GamePhase.Day:
                BuildFilledOrdersSection(sb);
                break;
            case GamePhase.PostMarket:
                BuildPostMarketSection(sb);
                break;
        }

        BuildHoldingsSection(sb);
        holdingsText.text = sb.ToString().TrimEnd();
    }

    private void BuildPendingOrdersSection(System.Text.StringBuilder sb)
    {
        if (GamePhaseManager.Inst == null) return;
        var orders = GamePhaseManager.Inst.PendingOrders;
        if (orders.Count == 0) return;

        sb.AppendLine("<b>--- Pending Orders ---</b>");
        foreach (var o in orders)
            sb.AppendLine($"  {o.side.ToUpper()} {o.quantity} {o.ticker}");
        sb.AppendLine();
    }

    private void BuildFilledOrdersSection(System.Text.StringBuilder sb)
    {
        if (GamePhaseManager.Inst == null) return;
        var results = GamePhaseManager.Inst.TodayResults;

        if (results.Count == 0)
        {
            sb.AppendLine("No trades placed today.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("<b>--- Today's Fills ---</b>");
        foreach (var r in results)
        {
            if (r.status == "ok")
                sb.AppendLine($"  {r.side.ToUpper()} {r.quantity} {r.ticker} @ ${r.fillPrice:F2}");
            else
                sb.AppendLine($"  {r.side.ToUpper()} {r.quantity} {r.ticker} - FAILED");
        }
        sb.AppendLine();
    }

    private void BuildPostMarketSection(System.Text.StringBuilder sb)
    {
        if (GamePhaseManager.Inst == null) return;
        var results = GamePhaseManager.Inst.TodayResults;

        if (results.Count == 0)
        {
            sb.AppendLine("No trades were placed today.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("<b>--- Trade Results ---</b>");
        float totalPnl = 0f;

        foreach (var r in results)
        {
            if (r.status != "ok")
            {
                sb.AppendLine($"  {r.side.ToUpper()} {r.quantity} {r.ticker} - FAILED");
                continue;
            }

            string pnlColor = r.pnl >= 0 ? "#26BF59" : "#D93838";
            string pnlSign = r.pnl >= 0 ? "+" : "-";
            string pnlStr = $"<color={pnlColor}>{pnlSign}${Mathf.Abs(r.pnl):F2}</color>";

            sb.AppendLine($"  {r.side.ToUpper()} {r.quantity} {r.ticker}");
            sb.AppendLine($"    Open: ${r.fillPrice:F2}  Close: ${r.closePrice:F2}  {pnlStr}");
            totalPnl += r.pnl;
        }

        sb.AppendLine();
        string totalColor = totalPnl >= 0 ? "#26BF59" : "#D93838";
        string totalSign = totalPnl >= 0 ? "+" : "-";
        sb.AppendLine($"<b>Day P&L: <color={totalColor}>{totalSign}${Mathf.Abs(totalPnl):F2}</color></b>");
        sb.AppendLine();
    }

    private void BuildHoldingsSection(System.Text.StringBuilder sb)
    {
        if (cachedHoldings != null && cachedHoldings.Length > 0)
        {
            sb.AppendLine("<b>--- Holdings ---</b>");
            foreach (var t in cachedHoldings)
                sb.AppendLine($"  {t.ticker_id}: {t.shares_held:F0} shares");
        }
        else
        {
            sb.AppendLine("No holdings");
        }
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

        SetStatus($"New day: {currentGameDate}");
    }

    // ── Refresh Helpers ──

    private async Task RefreshPrice()
    {
        if (string.IsNullOrEmpty(selectedTicker) || string.IsNullOrEmpty(currentGameDate))
        {
            priceText.text = "---";
            if (ohlcChart != null) ohlcChart.Clear();
            return;
        }

        try
        {
            // During Day phase, don't show today's candle (outcome unknown)
            string chartEndDate = currentGameDate;
            if (currentPhase == GamePhase.Day)
            {
                if (System.DateTime.TryParse(currentGameDate, out var dt))
                    chartEndDate = dt.AddDays(-1).ToString("yyyy-MM-dd");
            }

            string startDate = null;
            if (System.DateTime.TryParse(chartEndDate, out var endDt))
                startDate = endDt.AddDays(-chartLookbackDays).ToString("yyyy-MM-dd");

            var resp = await MarketAPI.GetPrices(selectedTicker, startDate, chartEndDate);
            if (resp.rows != null && resp.rows.Length > 0)
            {
                if (ohlcChart != null) ohlcChart.SetData(resp.rows);

                var latest = resp.rows[resp.rows.Length - 1];

                switch (currentPhase)
                {
                    case GamePhase.PreMarket:
                        var todayResp = await MarketAPI.GetPrices(selectedTicker, currentGameDate, currentGameDate);
                        if (todayResp.rows != null && todayResp.rows.Length > 0)
                        {
                            var today = todayResp.rows[0];
                            priceText.text = $"Today's Open: ${today.open_price:F2}   (Prev Close: ${latest.close_price:F2})";
                        }
                        else
                        {
                            priceText.text = $"Prev Close: ${latest.close_price:F2}";
                        }
                        break;

                    case GamePhase.Day:
                        priceText.text = $"Prev Close: ${latest.close_price:F2}   (Markets open)";
                        break;

                    case GamePhase.PostMarket:
                        var pmResp = await MarketAPI.GetPrices(selectedTicker, currentGameDate, currentGameDate);
                        if (pmResp.rows != null && pmResp.rows.Length > 0)
                        {
                            var p = pmResp.rows[0];
                            priceText.text = $"O: ${p.open_price:F2}   H: ${p.high_price:F2}   L: ${p.low_price:F2}   C: ${p.close_price:F2}";

                            // Add today's candle to the chart
                            if (ohlcChart != null)
                            {
                                var fullResp = await MarketAPI.GetPrices(selectedTicker, startDate, currentGameDate);
                                if (fullResp.rows != null && fullResp.rows.Length > 0)
                                    ohlcChart.SetData(fullResp.rows);
                            }
                        }
                        else
                        {
                            priceText.text = $"Close: ${latest.close_price:F2}";
                        }
                        break;
                }
            }
            else
            {
                if (ohlcChart != null) ohlcChart.Clear();
                priceText.text = "No price data for this date";
            }
        }
        catch
        {
            priceText.text = "Failed to load price";
        }
    }

    private async Task RefreshPortfolio()
    {
        try
        {
            var resp = await TradeAPI.GetPortfolio(APIBootstrapper.EntityDbId);
            cashText.text = $"Cash: ${resp.entity.available_cash:N2}";
            cachedHoldings = resp.totals;
        }
        catch
        {
            cashText.text = "Cash: ---";
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
