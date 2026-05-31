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
    [SerializeField] private TMP_InputField tickerSearchInput;
    [SerializeField] private int searchResultLimit = 8;
    private GameObject searchResultsPanel;
    private List<GameObject> searchResultItems = new List<GameObject>();
    private bool searchIsOpen;

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
    private static string selectedTicker;
    private string currentGameDate;
    private GamePhase currentPhase;
    private PortfolioTotalDTO[] cachedHoldings;
    private PortfolioLotDTO[] cachedLots;
    private ChartTimeframe selectedTimeframe = ChartTimeframe.Month3;
    private bool showingPortfolioChart = false;
    private BottomTab activeTab = BottomTab.Trade;
    private bool tickerTradableToday = true;
    private double currentEstimatedPrice;
    private double cachedServerNetWorth;
    private double cachedServerCash;
    private double cachedServerHoldingsValue;

    private bool confirmTradesPending;
    private Coroutine confirmResetCoroutine;
    private string savedAdvanceButtonText;
    private NetWorthPointDTO[] cachedPortfolioHistory;
    private string prevCloseCacheDate;
    private readonly Dictionary<string, double> prevCloseCache = new Dictionary<string, double>();

    private GameObject dismissBg;

    private bool hasOpenedBefore;

    public void Open()
    {
        ShowDismissBackground();
        tradingPanel.SetActive(true);
        PlayerStateController.Inst.OpenUI(PlayerState.TRADING, ClosePanel);

        closeButton.onClick.AddListener(Close);
        if (tickerDropdown != null && tickerSearchInput == null)
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
        ProgressionGates.OnGatesChanged += ApplyProgressionGates;

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
        if (tickerDropdown != null) tickerDropdown.onValueChanged.RemoveAllListeners();
        if (tickerSearchInput != null)
        {
            tickerSearchInput.onValueChanged.RemoveAllListeners();
            tickerSearchInput.onSelect.RemoveAllListeners();
            tickerSearchInput.onDeselect.RemoveAllListeners();
            CloseSearchResults();
        }
        ProgressionGates.OnGatesChanged -= ApplyProgressionGates;
        UnbindTimeframeButtons();
        UnbindChartTabs();
        UnbindBottomTabs();

        HideDismissBackground();
        tradingPanel.SetActive(false);
    }

    private void ShowDismissBackground()
    {
        if (dismissBg == null)
        {
            var parentCanvas = tradingPanel.GetComponentInParent<Canvas>();
            if (parentCanvas == null) return;

            dismissBg = new GameObject("DismissBackground");
            dismissBg.transform.SetParent(parentCanvas.transform, false);
            var rt = dismissBg.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            var img = dismissBg.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.4f);
            var btn = dismissBg.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Close);
        }

        dismissBg.SetActive(true);
        dismissBg.transform.SetAsFirstSibling();
    }

    private void HideDismissBackground()
    {
        if (dismissBg != null)
            dismissBg.SetActive(false);
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
        ApplyProgressionGates();
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

        if (tickerSearchInput != null)
        {
            BuildSearchResultsPanel();
            tickerSearchInput.onValueChanged.RemoveAllListeners();
            tickerSearchInput.onValueChanged.AddListener(OnSearchTyping);
            tickerSearchInput.onSelect.AddListener(_ => OnSearchFocused());
            tickerSearchInput.onDeselect.AddListener(_ => DelayedCloseSearch());
            tickerSearchInput.onSubmit.AddListener(_ => OnSearchSubmit());

            if (!string.IsNullOrEmpty(selectedTicker))
                tickerSearchInput.SetTextWithoutNotify(FormatTickerLabel(selectedTicker));
            else if (tickers != null && tickers.Length > 0)
                tickerSearchInput.SetTextWithoutNotify(FormatTickerLabel(tickers[0].ticker_id));

            if (tickerDropdown != null)
                tickerDropdown.gameObject.SetActive(false);
        }
        else if (tickerDropdown != null)
        {
            tickerDropdown.ClearOptions();
            var options = new List<string>();
            if (tickers != null)
                foreach (var t in tickers)
                    options.Add(string.IsNullOrEmpty(t.company_name) ? t.ticker_id : $"{t.ticker_id} — {t.company_name}");
            tickerDropdown.AddOptions(options);
        }

        if (tickers != null && tickers.Length > 0)
        {
            if (string.IsNullOrEmpty(selectedTicker))
                selectedTicker = tickers[0].ticker_id;
            int idx = System.Array.FindIndex(tickers, t => t.ticker_id == selectedTicker);
            if (idx < 0) { idx = 0; selectedTicker = tickers[0].ticker_id; }
            if (tickerDropdown != null && tickerSearchInput == null)
                tickerDropdown.SetValueWithoutNotify(idx);
            UpdateCompanyName(idx);
            await RefreshPrice();
        }
    }

    private string FormatTickerLabel(string tickerId)
    {
        if (tickers == null) return tickerId;
        var t = System.Array.Find(tickers, x => x.ticker_id == tickerId);
        if (t == null) return tickerId;
        return string.IsNullOrEmpty(t.company_name) ? t.ticker_id : $"{t.ticker_id} — {t.company_name}";
    }

    private void BuildSearchResultsPanel()
    {
        if (searchResultsPanel != null) return;

        var inputRT = tickerSearchInput.GetComponent<RectTransform>();
        var canvas = tickerSearchInput.GetComponentInParent<Canvas>();

        searchResultsPanel = new GameObject("SearchResults", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.VerticalLayoutGroup),
            typeof(UnityEngine.UI.ContentSizeFitter));

        searchResultsPanel.transform.SetParent(inputRT, false);

        var rt = searchResultsPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -inputRT.rect.height);
        rt.sizeDelta = new Vector2(0, 0);

        var bg = searchResultsPanel.GetComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0.14f, 0.15f, 0.18f, 0.97f);

        var vlg = searchResultsPanel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.spacing = 2;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        var csf = searchResultsPanel.GetComponent<UnityEngine.UI.ContentSizeFitter>();
        csf.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

        if (canvas != null)
        {
            var canvasGO = new GameObject("SearchResultsCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasGO.transform.SetParent(inputRT, false);
            var crt = canvasGO.GetComponent<RectTransform>();
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.sizeDelta = Vector2.zero;
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            var c = canvasGO.GetComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = 100;
            searchResultsPanel.transform.SetParent(canvasGO.transform, false);
            rt.anchoredPosition = new Vector2(0, -inputRT.rect.height);
        }

        searchResultsPanel.SetActive(false);
    }

    private void OnSearchFocused()
    {
        if (tickerSearchInput != null)
            tickerSearchInput.SetTextWithoutNotify("");
        ShowSearchResults("");
    }

    private void OnSearchSubmit()
    {
        if (!searchIsOpen || searchResultItems.Count == 0) return;
        var btn = searchResultItems[0].GetComponent<UnityEngine.UI.Button>();
        if (btn != null) btn.onClick.Invoke();
    }

    private void OnSearchTyping(string query)
    {
        ShowSearchResults(query);
    }

    private void ShowSearchResults(string query)
    {
        if (searchResultsPanel == null || tickers == null) return;

        foreach (var item in searchResultItems)
            Destroy(item);
        searchResultItems.Clear();

        var sourceFont = tickerSearchInput.textComponent.font;
        var sourceMat = tickerSearchInput.textComponent.fontSharedMaterial;

        string upper = string.IsNullOrEmpty(query) ? null : query.ToUpperInvariant();
        int count = 0;
        int totalMatches = 0;

        // Count total matches first
        if (upper != null)
        {
            for (int i = 0; i < tickers.Length; i++)
            {
                var t = tickers[i];
                bool match = (t.ticker_id != null && t.ticker_id.ToUpperInvariant().Contains(upper))
                          || (t.company_name != null && t.company_name.ToUpperInvariant().Contains(upper));
                if (match) totalMatches++;
            }
        }
        else
        {
            totalMatches = tickers.Length;
        }

        for (int i = 0; i < tickers.Length && count < searchResultLimit; i++)
        {
            var t = tickers[i];
            if (upper != null)
            {
                bool match = (t.ticker_id != null && t.ticker_id.ToUpperInvariant().Contains(upper))
                          || (t.company_name != null && t.company_name.ToUpperInvariant().Contains(upper));
                if (!match) continue;
            }

            var itemGO = new GameObject("Result", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button),
                typeof(UnityEngine.UI.LayoutElement));

            itemGO.transform.SetParent(searchResultsPanel.transform, false);

            var le = itemGO.GetComponent<UnityEngine.UI.LayoutElement>();
            le.preferredHeight = 28;

            var itemBg = itemGO.GetComponent<UnityEngine.UI.Image>();
            itemBg.color = new Color(0.18f, 0.19f, 0.24f, 1f);

            var textGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
            textGO.transform.SetParent(itemGO.transform, false);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(8, 0);
            textRT.offsetMax = new Vector2(-8, 0);
            tmp.font = sourceFont;
            tmp.fontSharedMaterial = sourceMat;
            tmp.fontSize = 14;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;
            tmp.richText = true;
            tmp.text = string.IsNullOrEmpty(t.company_name)
                ? t.ticker_id
                : $"<b>{t.ticker_id}</b>  <color=#AAAAAA>{t.company_name}</color>";

            int capturedIndex = i;
            var btn = itemGO.GetComponent<UnityEngine.UI.Button>();
            btn.onClick.AddListener(() => SelectTicker(capturedIndex));

            searchResultItems.Add(itemGO);
            count++;
        }

        // Show truncation label if results were capped
        if (totalMatches > searchResultLimit)
        {
            var countGO = new GameObject("ResultCount", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(UnityEngine.UI.LayoutElement));
            countGO.transform.SetParent(searchResultsPanel.transform, false);
            var cle = countGO.GetComponent<UnityEngine.UI.LayoutElement>();
            cle.preferredHeight = 24;
            var countText = countGO.AddComponent<TextMeshProUGUI>();
            countText.font = sourceFont;
            countText.fontSharedMaterial = sourceMat;
            countText.fontSize = 12;
            countText.alignment = TextAlignmentOptions.MidlineLeft;
            countText.color = new Color(0.55f, 0.55f, 0.6f);
            countText.text = $"  Showing {searchResultLimit} of {totalMatches} — keep typing to narrow";
            countText.raycastTarget = false;
            searchResultItems.Add(countGO);
        }

        searchResultsPanel.SetActive(count > 0);
        searchIsOpen = count > 0;
    }

    private void SelectTicker(int index)
    {
        if (tickers == null || index >= tickers.Length) return;
        selectedTicker = tickers[index].ticker_id;
        UpdateCompanyName(index);

        if (tickerSearchInput != null)
            tickerSearchInput.SetTextWithoutNotify(FormatTickerLabel(selectedTicker));

        CloseSearchResults();
        _ = RefreshPrice();
    }

    private void CloseSearchResults()
    {
        if (searchResultsPanel != null) searchResultsPanel.SetActive(false);
        searchIsOpen = false;
    }

    private void DelayedCloseSearch()
    {
        StartCoroutine(CloseSearchAfterDelay());
    }

    private System.Collections.IEnumerator CloseSearchAfterDelay()
    {
        yield return new WaitForSeconds(0.15f);
        CloseSearchResults();
        if (tickerSearchInput != null && !string.IsNullOrEmpty(selectedTicker))
            tickerSearchInput.SetTextWithoutNotify(FormatTickerLabel(selectedTicker));
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

            case GamePhase.PostMarket:
                buyButton.gameObject.SetActive(true);
                sellButton.gameObject.SetActive(true);
                quantityInput.gameObject.SetActive(true);
                if (advanceDayButton != null)
                {
                    advanceDayButton.gameObject.SetActive(true);
                    advanceDayButton.interactable = true;
                    advanceDayButton.onClick.AddListener(OnConfirmPostMarketOrders);
                    SetAdvanceButtonText(GamePhaseManager.Inst != null
                        && GamePhaseManager.Inst.PendingOrders.Count > 0
                        ? "Confirm Trades" : "End Day");
                }

                buyButton.onClick.AddListener(OnQueueBuy);
                sellButton.onClick.AddListener(OnQueueSell);
                break;

            case GamePhase.Day:
                buyButton.gameObject.SetActive(false);
                sellButton.gameObject.SetActive(false);
                quantityInput.gameObject.SetActive(false);
                if (advanceDayButton != null)
                    advanceDayButton.gameObject.SetActive(false);
                break;
        }
    }

    // ── Progression Gating ──

    private void ApplyProgressionGates()
    {
        if (netWorthText != null)
            netWorthText.gameObject.SetActive(ProgressionGates.ShowNetWorth);

        if (ohlcChart != null)
            ohlcChart.ShowWicks = ProgressionGates.ShowWicks;

        if (portfolioChartButton != null)
            portfolioChartButton.gameObject.SetActive(ProgressionGates.ShowPortfolioChart);

        if (!ProgressionGates.ShowPortfolioChart && showingPortfolioChart)
        {
            showingPortfolioChart = false;
            SetChartVisibility();
        }

        ApplyTimeframeGates();
    }

    private void ApplyTimeframeGates()
    {
        bool all = ProgressionGates.ShowAllTimeframes;
        if (btn1W != null) btn1W.gameObject.SetActive(all);
        if (btn3M != null) btn3M.gameObject.SetActive(all);
        if (btn1Y != null) btn1Y.gameObject.SetActive(all);
        if (btn5Y != null) btn5Y.gameObject.SetActive(all);

        if (!all && selectedTimeframe != ChartTimeframe.Month1)
            SetTimeframe(ChartTimeframe.Month1);
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
                    var pmResults = GamePhaseManager.Inst.TodayResults;
                    if (pmResults.Count > 0)
                    {
                        double totalPnl = 0;
                        foreach (var r in pmResults)
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

                    var pmFills = GamePhaseManager.Inst.PostMarketResults;
                    if (pmFills.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("<b>Post-market fills:</b>");
                        foreach (var r in pmFills)
                        {
                            string sideColor = r.side == "buy" ? "#26BF59" : "#D93838";
                            if (r.status == "ok")
                                sb.AppendLine($"<color={sideColor}>{r.side.ToUpper()}</color>  {r.quantity} {r.ticker} @ ${FmtPrice(r.fillPrice)}");
                            else
                                sb.AppendLine($"<color={sideColor}>{r.side.ToUpper()}</color>  {r.quantity} {r.ticker} — <color=#D93838>{r.message ?? "FAILED"}</color>");
                        }
                    }

                    var pmOrders = GamePhaseManager.Inst.PendingOrders;
                    if (pmOrders.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("<b>Pending orders:</b>");
                        foreach (var o in pmOrders)
                        {
                            string sideColor = o.side == "buy" ? "#26BF59" : "#D93838";
                            sb.AppendLine($"<color={sideColor}>{o.side.ToUpper()}</color>  {o.quantity} {o.ticker}  ~${FmtPrice(o.estimatedPrice)}");
                        }
                    }
                }
                break;
        }

        if (cachedHoldings != null && cachedHoldings.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<color=#666D78>─────────────────────────</color>");
            sb.AppendLine("<b><color=#666D78>POSITIONS</color></b>");
            double totalDayPnl = 0;
            foreach (var h in cachedHoldings)
            {
                prevCloseCache.TryGetValue(h.ticker_id, out double prevClose);
                double dayChange = prevClose > 0 ? h.current_price - prevClose : 0;
                double dayChangePct = prevClose > 0 ? (dayChange / prevClose) * 100 : 0;
                double posDayPnl = dayChange * h.shares_held;
                totalDayPnl += posDayPnl;

                string arrow = dayChange >= 0 ? "▲" : "▼";
                string changeColor = dayChange >= 0 ? "#26BF59" : "#D93838";
                string sign = dayChange >= 0 ? "+" : "";

                sb.AppendLine($"<b>{h.ticker_id}</b>  {h.shares_held:F0} shares  ${FmtPrice(h.current_price)}");
                if (prevClose > 0)
                    sb.AppendLine($"  <color={changeColor}>{arrow} {sign}{dayChangePct:F1}%  {sign}${FmtPrice(posDayPnl)}</color>");
            }
            string ptColor = totalDayPnl >= 0 ? "#26BF59" : "#D93838";
            string ptSign = totalDayPnl >= 0 ? "+" : "";
            sb.AppendLine($"\n<b>Positions P&L:  <color={ptColor}>{ptSign}${FmtPrice(totalDayPnl)}</color></b>");
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

                if (ProgressionGates.ShowCostBasis)
                {
                    double totalCost = 0, totalShares = 0;
                    if (cachedLots != null)
                    {
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

                    if (h.market_value > 0)
                    {
                        sb.Append($"  ·  Val ${h.market_value:N2}");
                        if (totalCost > 0)
                        {
                            double gain = h.market_value - totalCost;
                            string gainColor = gain >= 0 ? "#26BF59" : "#D93838";
                            string gainSign = gain >= 0 ? "+" : "";
                            sb.Append($"  <color={gainColor}>{gainSign}${gain:N2}</color>");
                        }
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
            if (advanceDayButton != null && (currentPhase == GamePhase.PreMarket || currentPhase == GamePhase.PostMarket))
            {
                advanceDayButton.interactable = true;
                if (currentPhase == GamePhase.PostMarket)
                    SetAdvanceButtonText("Confirm Trades");
            }
        }
        else
        {
            SetStatus(GamePhaseManager.Inst.LastOrderError ?? "Failed to queue order.");
        }
    }

    // ── Phase Transitions ──

    private async void OnConfirmPostMarketOrders()
    {
        if (GamePhaseManager.Inst == null) return;

        bool isEndDay = GamePhaseManager.Inst.PendingOrders.Count == 0;
        if (GameSettings.RequireDoubleConfirm && !confirmTradesPending)
        {
            confirmTradesPending = true;
            savedAdvanceButtonText = advanceButtonText != null ? advanceButtonText.text : "";
            SetAdvanceButtonText(isEndDay ? "Are you sure? Click again" : "Confirm? Click again");
            if (confirmResetCoroutine != null) StopCoroutine(confirmResetCoroutine);
            confirmResetCoroutine = StartCoroutine(ResetConfirmAfterDelay());
            return;
        }
        ResetConfirmState();

        SetStatus("Confirming trades...");
        advanceDayButton.interactable = false;

        var results = await GamePhaseManager.Inst.ConfirmPostMarketOrders();
        if (results == null)
        {
            SetStatus("Failed to confirm trades.");
            advanceDayButton.interactable = true;
            return;
        }

        int filled = 0, failed = 0;
        foreach (var r in results)
        {
            if (r.status == "ok") filled++;
            else failed++;
        }

        string msg = filled > 0 ? $"{filled} order(s) filled at close." : "No orders filled.";
        if (failed > 0) msg += $" {failed} failed.";
        SetStatus(msg);

        await RefreshPortfolio();
        if (showingPortfolioChart)
            await LoadPortfolioChart();
        RefreshOrdersDisplay();

        Close();
    }

    private async void OnOpenMarkets()
    {
        if (GamePhaseManager.Inst == null) return;

        if (GameSettings.RequireDoubleConfirm && !confirmTradesPending)
        {
            confirmTradesPending = true;
            savedAdvanceButtonText = advanceButtonText != null ? advanceButtonText.text : "";
            SetAdvanceButtonText("Confirm? Click again");
            if (confirmResetCoroutine != null) StopCoroutine(confirmResetCoroutine);
            confirmResetCoroutine = StartCoroutine(ResetConfirmAfterDelay());
            return;
        }
        ResetConfirmState();

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

    private System.Collections.IEnumerator ResetConfirmAfterDelay()
    {
        yield return new UnityEngine.WaitForSeconds(3f);
        ResetConfirmState();
    }

    private void ResetConfirmState()
    {
        confirmTradesPending = false;
        if (confirmResetCoroutine != null)
        {
            StopCoroutine(confirmResetCoroutine);
            confirmResetCoroutine = null;
        }
        if (savedAdvanceButtonText != null && advanceButtonText != null)
        {
            SetAdvanceButtonText(savedAdvanceButtonText);
            savedAdvanceButtonText = null;
        }
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
            var history = resp.history;

            if (history != null && !string.IsNullOrEmpty(currentGameDate))
            {
                var livePoint = new NetWorthPointDTO
                {
                    date = currentGameDate + "T16:00",
                    cash = cachedServerCash,
                    holdings_value = cachedServerHoldingsValue,
                    net_worth = cachedServerNetWorth
                };

                bool replaced = false;
                for (int i = history.Length - 1; i >= 0; i--)
                {
                    if (history[i].date == livePoint.date)
                    {
                        history[i] = livePoint;
                        replaced = true;
                        break;
                    }
                }
                if (!replaced)
                {
                    var extended = new NetWorthPointDTO[history.Length + 1];
                    System.Array.Copy(history, extended, history.Length);
                    extended[history.Length] = livePoint;
                    history = extended;
                }
            }

            cachedPortfolioHistory = history;
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

            int max = CandleAggregator.MaxPortfolioPoints(selectedTimeframe);
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
            if (currentPhase == GamePhase.PreMarket || currentPhase == GamePhase.Day)
            {
                if (System.DateTime.TryParse(currentGameDate, out var dt))
                    chartEndDate = dt.AddDays(-1).ToString("yyyy-MM-dd");
            }
            // PostMarket: chartEndDate stays as currentGameDate to include today's full OHLC

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
                    case GamePhase.Day:
                        var todayResp = await MarketAPI.GetPrices(selectedTicker, currentGameDate, currentGameDate);
                        if (todayResp.rows != null && todayResp.rows.Length > 0)
                        {
                            double openPrice = todayResp.rows[0].open_price;
                            currentEstimatedPrice = openPrice;
                            double dayChange = openPrice - prevClose.close_price;
                            double dayChangePct = prevClose.close_price > 0
                                ? dayChange / prevClose.close_price * 100.0 : 0;
                            string sign = dayChange >= 0 ? "+" : "";
                            string clr = dayChange >= 0 ? "#26BF59" : "#D93838";
                            priceText.text = ProgressionGates.ShowPriceChange
                                ? $"${FmtPrice(openPrice)}   <color={clr}>{sign}{FmtPrice(dayChange)} ({sign}{dayChangePct:F1}%)</color>"
                                : $"${FmtPrice(openPrice)}";

                            if (ohlcChart != null && !showingPortfolioChart)
                            {
                                var currentBar = new PriceRowDTO
                                {
                                    ticker_id = selectedTicker,
                                    date = currentGameDate,
                                    open_price = openPrice,
                                    high_price = openPrice,
                                    low_price = openPrice,
                                    close_price = openPrice
                                };
                                var extended = new PriceRowDTO[chartData.Length + 1];
                                System.Array.Copy(chartData, extended, chartData.Length);
                                extended[chartData.Length] = currentBar;
                                ohlcChart.SetData(extended);
                            }

                            SetTickerTradable(currentPhase == GamePhase.PreMarket);
                        }
                        else
                        {
                            priceText.text = $"${FmtPrice(prevClose.close_price)}   <color=#FF8800>(halted/delisted)</color>";
                            SetTickerTradable(false);
                        }
                        break;

                    case GamePhase.PostMarket:
                        var todayRow = resp.rows[resp.rows.Length - 1];
                        var yesterdayClose = resp.rows.Length >= 2
                            ? resp.rows[resp.rows.Length - 2].close_price
                            : todayRow.open_price;
                        if (todayRow.date == currentGameDate)
                        {
                            currentEstimatedPrice = todayRow.close_price;
                            double change = todayRow.close_price - yesterdayClose;
                            double changePct = yesterdayClose > 0
                                ? change / yesterdayClose * 100.0 : 0;
                            string sign = change >= 0 ? "+" : "";
                            string color = change >= 0 ? "#26BF59" : "#D93838";
                            priceText.text = ProgressionGates.ShowPriceChange
                                ? $"${FmtPrice(todayRow.close_price)}   <color={color}>{sign}{FmtPrice(change)} ({sign}{changePct:F1}%)</color>"
                                : $"${FmtPrice(todayRow.close_price)}";

                            if (ohlcChart != null && !showingPortfolioChart)
                            {
                                var closeBar = new PriceRowDTO
                                {
                                    ticker_id = selectedTicker,
                                    date = currentGameDate,
                                    open_price = todayRow.close_price,
                                    high_price = todayRow.close_price,
                                    low_price = todayRow.close_price,
                                    close_price = todayRow.close_price
                                };
                                var extended = new PriceRowDTO[chartData.Length + 1];
                                System.Array.Copy(chartData, extended, chartData.Length);
                                extended[chartData.Length] = closeBar;
                                ohlcChart.SetData(extended);
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
        if (currentPhase == GamePhase.PreMarket || currentPhase == GamePhase.PostMarket)
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
            cachedServerNetWorth = resp.net_worth;
            cachedServerCash = cash;
            cachedServerHoldingsValue = resp.holdings_value;

            if (GamePhaseManager.Inst != null)
            {
                GamePhaseManager.Inst.SetServerCash(cash);
                GamePhaseManager.Inst.SetServerNetWorth(resp.net_worth);
            }

            RefreshCashDisplay();

            if (netWorthText != null)
            {
                double reservedCost = GamePhaseManager.Inst != null
                    ? GamePhaseManager.Inst.ReservedBuyCost : 0;
                double netWorth = resp.net_worth - reservedCost;
                string color = netWorth >= 10000 ? "#26BF59" : "#D93838";
                if (ProgressionGates.ShowCashBreakdown)
                    netWorthText.text = $"Net Worth: <color={color}>${netWorth:N2}</color>  (Holdings ${cachedServerHoldingsValue:N2})";
                else
                    netWorthText.text = $"Net Worth: <color={color}>${netWorth:N2}</color>";
            }

            RefreshHoldingsDisplay();

            if (currentGameDate != prevCloseCacheDate)
            {
                await FetchPrevClosePrices();
                prevCloseCacheDate = currentGameDate;
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

    private async Task FetchPrevClosePrices()
    {
        prevCloseCache.Clear();
        if (cachedHoldings == null || string.IsNullOrEmpty(currentGameDate)) return;

        foreach (var h in cachedHoldings)
        {
            try
            {
                string startDate = currentGameDate;
                if (System.DateTime.TryParse(currentGameDate, out var dt))
                    startDate = dt.AddDays(-10).ToString("yyyy-MM-dd");
                var resp = await MarketAPI.GetPrices(h.ticker_id, startDate, currentGameDate);
                if (resp.rows != null && resp.rows.Length >= 2)
                    prevCloseCache[h.ticker_id] = resp.rows[resp.rows.Length - 2].close_price;
            }
            catch { }
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
