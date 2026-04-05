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

    [Header("Player Info")]
    [SerializeField] private TMP_Text cashText;
    [SerializeField] private TMP_Text holdingsText;

    [Header("Feedback")]
    [SerializeField] private TMP_Text statusText;

    private TickerDTO[] tickers;
    private string selectedTicker;
    private string currentGameDate;

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
        // React to leaving TRADING (triggered by Escape in PlayerStateController)
        if (oldState == PlayerState.TRADING && tradingPanel.activeSelf)
            ClosePanel();
    }

    /// <summary>Call from InteractionZone or other trigger to open the terminal.</summary>
    public void Open()
    {
        tradingPanel.SetActive(true);
        PlayerStateController.Inst.SetState(PlayerState.TRADING);

        closeButton.onClick.AddListener(Close);
        buyButton.onClick.AddListener(OnBuy);
        sellButton.onClick.AddListener(OnSell);
        advanceDayButton.onClick.AddListener(OnAdvanceDay);
        tickerDropdown.onValueChanged.AddListener(OnTickerChanged);

        _ = LoadInitialData();
    }

    /// <summary>Called by the close button — triggers state change, which triggers ClosePanel via event.</summary>
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

    // ---- Data Loading ----

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
            await RefreshGameDate();
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

        SetStatus("Ready");
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

    // ---- Refresh Helpers ----

    private async Task RefreshGameDate()
    {
        var resp = await GameStateAPI.GetGameDate();
        currentGameDate = resp.current_date;
        dateText.text = currentGameDate ?? "No active game";
    }

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
            // Fetch a window of history ending at the current game date (no future data)
            string startDate = null;
            if (System.DateTime.TryParse(currentGameDate, out var endDt))
                startDate = endDt.AddDays(-chartLookbackDays).ToString("yyyy-MM-dd");

            var resp = await MarketAPI.GetPrices(selectedTicker, startDate, currentGameDate);

            if (resp.rows != null && resp.rows.Length > 0)
            {
                // Feed full history to the chart
                if (ohlcChart != null) ohlcChart.SetData(resp.rows);

                // Show today's values as text
                var p = resp.rows[resp.rows.Length - 1];
                priceText.text = $"O: {p.open_price:F2}   H: {p.high_price:F2}   L: {p.low_price:F2}   C: {p.close_price:F2}";
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

            if (resp.totals != null && resp.totals.Length > 0)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var t in resp.totals)
                    sb.AppendLine($"{t.ticker_id}: {t.shares_held:F0} shares");
                holdingsText.text = sb.ToString().TrimEnd();
            }
            else
            {
                holdingsText.text = "No holdings";
            }
        }
        catch
        {
            cashText.text = "Cash: ---";
            holdingsText.text = "---";
        }
    }

    // ---- Trade Execution ----

    private async void OnBuy() => await ExecuteTrade("buy");
    private async void OnSell() => await ExecuteTrade("sell");

    private async Task ExecuteTrade(string side)
    {
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

        SetStatus($"Placing {side} order...");

        var req = new TradeRequestDTO
        {
            entity_id = APIBootstrapper.EntityExternalId,
            ticker = selectedTicker,
            side = side,
            quantity = qty,
            order_type = "market"
        };

        try
        {
            var resp = await TradeAPI.PostTrade(req);
            if (resp.status == "ok")
                SetStatus($"{side.ToUpper()} {qty} {selectedTicker} - Success!");
            else
                SetStatus($"Error: {resp.message ?? resp.status}");

            await RefreshPortfolio();
            await RefreshPrice();
        }
        catch (System.Exception ex)
        {
            SetStatus($"Trade failed: {ex.Message}");
        }
    }

    // ---- Game Day ----

    private async void OnAdvanceDay()
    {
        SetStatus("Advancing day...");
        try
        {
            var resp = await GameStateAPI.AdvanceDay();
            if (resp.game_over)
            {
                SetStatus("Game over - no more trading days.");
                return;
            }

            currentGameDate = resp.current_date;
            dateText.text = currentGameDate;
            SetStatus($"Advanced to {currentGameDate}");

            await RefreshPrice();
            await RefreshPortfolio();
        }
        catch (System.Exception ex)
        {
            SetStatus($"Failed: {ex.Message}");
        }
    }

    // ---- UI Helpers ----

    private void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
    }
}
