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
    [SerializeField] private Transform ordersContainer;

    [Header("Order Type")]
    [SerializeField] private TMP_Dropdown orderTypeDropdown;
    [SerializeField] private TMP_InputField priceInput;
    [SerializeField] private TMP_Text priceInputLabel;
    private string selectedOrderType = "market";
    private readonly System.Collections.Generic.List<string> orderTypeKeys = new();

    [Header("Portfolio Tab")]
    [SerializeField] private TMP_Text holdingsText;

    [Header("History Tab")]
    [SerializeField] private TMP_Text historyText;

    [Header("Always Visible")]
    [SerializeField] private TMP_Text cashText;
    [SerializeField] private TMP_Text netWorthText;
    [SerializeField] private TMP_Text statusText;

    private enum BottomTab { Trade, Portfolio, History }

    private static readonly HashSet<string> starterTickers = new()
    {
        "AAPL", "GOOG", "AMZN", "MSFT", "JPM", "GE", "WMT", "DIS"
    };

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
    private readonly List<GameObject> orderRowObjects = new();

    public bool TutorialMode { get; set; }
    public bool TutorialTradeConfirmed { get; private set; }
    public bool IsDataLoaded { get; private set; }

    private bool tutorialOrderQueued;
    private string tutorialOrderTicker;
    private string tutorialOrderSide;
    private int tutorialOrderQty;

    private GameObject tutCaseyPanel;
    private TMP_Text tutCaseyBody;
    private bool tutCaseyTyping;
    private int tutCaseyTotalChars;
    private float tutCaseyCharAccum;
    private const float TutCaseyCharsPerSec = 50f;

    public void Open()
    {
        IsDataLoaded = false;
        TutorialTradeConfirmed = false;
        tutorialOrderQueued = false;
        if (!TutorialMode)
            ShowDismissBackground();
        tradingPanel.SetActive(true);
        if (!TutorialMode)
            PlayerStateController.Inst.OpenUI(PlayerState.TRADING, ClosePanel);

        if (TutorialMode)
            closeButton.gameObject.SetActive(false);
        else
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

        if (ohlcChart != null) ohlcChart.gameObject.SetActive(false);
        if (portfolioChart != null) portfolioChart.gameObject.SetActive(false);
        ProgressionGates.OnGatesChanged += ApplyProgressionGates;
        if (orderTypeDropdown != null)
        {
            RebuildOrderTypeDropdown();
            orderTypeDropdown.onValueChanged.AddListener(OnOrderTypeChanged);
        }
        UpdateOrderTypeUI();

        _ = LoadInitialData();
    }

    public void Close()
    {
        if (TutorialMode) { ClosePanel(); return; }
        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.MOVING);
    }

    public void ForceTutorialReady()
    {
        currentPhase = GamePhase.PreMarket;
        currentGameDate = "2007-04-02";
        currentEstimatedPrice = 97.14;

        if (tickers == null || tickers.Length == 0)
        {
            tickers = new TickerDTO[]
            {
                new() { ticker_id = "AAPL", company_name = "Apple Inc." },
                new() { ticker_id = "GOOG", company_name = "Google Inc." },
                new() { ticker_id = "MSFT", company_name = "Microsoft Corp." },
                new() { ticker_id = "AMZN", company_name = "Amazon.com Inc." },
                new() { ticker_id = "JPM", company_name = "JPMorgan Chase & Co." },
                new() { ticker_id = "GE", company_name = "General Electric Co." },
            };

            if (tickerSearchInput != null)
            {
                BuildSearchResultsPanel();
                tickerSearchInput.onValueChanged.RemoveAllListeners();
                tickerSearchInput.onValueChanged.AddListener(OnSearchTyping);
                tickerSearchInput.onSelect.AddListener(_ => OnSearchFocused());
                tickerSearchInput.onDeselect.AddListener(_ => DelayedCloseSearch());
                tickerSearchInput.onSubmit.AddListener(_ => OnSearchSubmit());
            }
            else if (tickerDropdown != null)
            {
                tickerDropdown.ClearOptions();
                var options = new List<string>();
                foreach (var t in tickers)
                    options.Add($"{t.ticker_id} — {t.company_name}");
                tickerDropdown.AddOptions(options);
            }
        }

        selectedTicker = "AAPL";
        if (tickerSearchInput != null)
            tickerSearchInput.SetTextWithoutNotify(FormatTickerLabel("AAPL"));
        else if (tickerDropdown != null)
            tickerDropdown.SetValueWithoutNotify(0);
        if (companyNameText != null)
            companyNameText.text = "Apple Inc.";
        if (priceText != null)
            priceText.text = "$97.14";
        if (dateText != null)
            dateText.text = FormatDateHeader();
        if (cashText != null)
            cashText.text = "Cash: $10,000.00";
        if (netWorthText != null)
            netWorthText.text = "Net Worth: <color=#26BF59>$10,000.00</color>";
        if (quantityInput != null)
            quantityInput.SetTextWithoutNotify("10");

        ApplyPhaseUI();
        SwitchTab(BottomTab.Trade);
        buyButton.interactable = true;
        sellButton.interactable = false;
        if (advanceDayButton != null) advanceDayButton.interactable = true;
        tickerTradableToday = true;
        IsDataLoaded = true;
    }

    private void ClosePanel()
    {
        closeButton.gameObject.SetActive(true);
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
        if (orderTypeDropdown != null)
            orderTypeDropdown.onValueChanged.RemoveAllListeners();
        UnbindTimeframeButtons();
        UnbindChartTabs();
        UnbindBottomTabs();

        ClearOrderRows();
        HideDismissBackground();
        HideTutCaseyPanel();
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

    // ── Order Type UI ──

    private void OnOrderTypeChanged(int index)
    {
        selectedOrderType = index >= 0 && index < orderTypeKeys.Count ? orderTypeKeys[index] : "market";
        UpdateOrderTypeUI();
    }

    private void RebuildOrderTypeDropdown()
    {
        orderTypeKeys.Clear();
        orderTypeKeys.Add("market");

        var labels = new System.Collections.Generic.List<string> { "Market" };

        // DISABLED: limit/stop order types commented out
        // if (ProgressionGates.ShowLimitOrders)
        // {
        //     orderTypeKeys.Add("limit");
        //     labels.Add("Limit");
        // }
        // if (ProgressionGates.ShowStopOrders)
        // {
        //     orderTypeKeys.Add("stop");
        //     labels.Add("Stop");
        // }
        // if (ProgressionGates.ShowStopLimitOrders)
        // {
        //     orderTypeKeys.Add("stop_limit");
        //     labels.Add("Stop-Limit");
        // }

        orderTypeDropdown.ClearOptions();
        orderTypeDropdown.AddOptions(labels);

        int idx = orderTypeKeys.IndexOf(selectedOrderType);
        if (idx < 0) { selectedOrderType = "market"; idx = 0; }
        orderTypeDropdown.SetValueWithoutNotify(idx);
    }

    private void UpdateOrderTypeUI()
    {
        // DISABLED: limit/stop order UI — always hide price input
        if (priceInput != null)
            priceInput.gameObject.SetActive(false);
        if (priceInputLabel != null)
            priceInputLabel.gameObject.SetActive(false);
        // bool needsPrice = selectedOrderType != "market";
        // if (priceInput != null)
        //     priceInput.gameObject.SetActive(needsPrice);
        // if (priceInputLabel != null)
        // {
        //     priceInputLabel.gameObject.SetActive(needsPrice);
        //     priceInputLabel.text = selectedOrderType switch
        //     {
        //         "limit" => "Limit Price",
        //         "stop" => "Stop Price",
        //         "stop_limit" => "Stop, Limit (e.g. 150,148)",
        //         _ => "Price"
        //     };
        // }
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

        SetChartVisibility();

        if (ShouldGuideFirstTrade())
            ApplyFirstTradeGuidance();
        else
            SetStatus("Ready");

        IsDataLoaded = true;
    }

    private bool isFirstTradeGuided;

    private bool ShouldGuideFirstTrade()
    {
        var kgm = KnowledgeGraphManager.Inst;
        if (kgm == null || !kgm.IsInitialized) return false;
        return !kgm.IsNodeCompleted("market_buy_sell");
    }

    private void ApplyFirstTradeGuidance()
    {
        isFirstTradeGuided = true;

        // Pre-select AAPL if available
        if (tickers != null)
        {
            int aaplIdx = System.Array.FindIndex(tickers, t => t.ticker_id == "AAPL");
            if (aaplIdx >= 0)
            {
                selectedTicker = "AAPL";
                if (tickerSearchInput != null)
                    tickerSearchInput.SetTextWithoutNotify(FormatTickerLabel("AAPL"));
                else if (tickerDropdown != null)
                    tickerDropdown.SetValueWithoutNotify(aaplIdx);
                UpdateCompanyName(aaplIdx);
                _ = RefreshPrice();
            }
        }

        // Pre-fill quantity
        if (quantityInput != null)
            quantityInput.SetTextWithoutNotify("10");

        SetStatus("Casey: Pick a stock and hit Buy to queue your first trade.");
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

        // Build ordered index: starter tickers first when query is empty
        List<int> displayOrder = new List<int>();
        if (upper == null)
        {
            for (int i = 0; i < tickers.Length; i++)
                if (tickers[i].ticker_id != null && starterTickers.Contains(tickers[i].ticker_id))
                    displayOrder.Add(i);
            for (int i = 0; i < tickers.Length; i++)
                if (tickers[i].ticker_id == null || !starterTickers.Contains(tickers[i].ticker_id))
                    displayOrder.Add(i);
            totalMatches = tickers.Length;
        }
        else
        {
            for (int i = 0; i < tickers.Length; i++)
            {
                var t = tickers[i];
                bool match = (t.ticker_id != null && t.ticker_id.ToUpperInvariant().Contains(upper))
                          || (t.company_name != null && t.company_name.ToUpperInvariant().Contains(upper));
                if (match) { displayOrder.Add(i); totalMatches++; }
            }
        }

        for (int di = 0; di < displayOrder.Count && count < searchResultLimit; di++)
        {
            int i = displayOrder[di];
            var t = tickers[i];

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
                    SetAdvanceButtonText("Confirm Trades");
                }

                buyButton.onClick.AddListener(OnQueueBuy);
                sellButton.onClick.AddListener(OnQueueSell);
                break;

            case GamePhase.Day:
                buyButton.gameObject.SetActive(false);
                sellButton.gameObject.SetActive(false);
                quantityInput.gameObject.SetActive(false);
                if (orderTypeDropdown != null) orderTypeDropdown.gameObject.SetActive(false);
                if (priceInput != null) priceInput.gameObject.SetActive(false);
                if (priceInputLabel != null) priceInputLabel.gameObject.SetActive(false);
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
        {
            ohlcChart.ShowCandlesticks = ProgressionGates.ShowCandlesticks;
            ohlcChart.ShowWicks = ProgressionGates.ShowWicks;
        }

        if (portfolioChartButton != null)
            portfolioChartButton.gameObject.SetActive(ProgressionGates.ShowPortfolioChart);

        if (!ProgressionGates.ShowPortfolioChart && showingPortfolioChart)
        {
            showingPortfolioChart = false;
            SetChartVisibility();
        }

        // DISABLED: limit/stop order dropdown always hidden
        if (orderTypeDropdown != null)
            orderTypeDropdown.gameObject.SetActive(false);
        selectedOrderType = "market";
        UpdateOrderTypeUI();
        // bool showOrders = ProgressionGates.ShowLimitOrders && currentPhase != GamePhase.Day;
        // if (orderTypeDropdown != null)
        // {
        //     orderTypeDropdown.gameObject.SetActive(showOrders);
        //     if (showOrders)
        //         RebuildOrderTypeDropdown();
        // }
        // if (!showOrders)
        // {
        //     selectedOrderType = "market";
        //     UpdateOrderTypeUI();
        // }

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

        ClearOrderRows();
        var sb = new System.Text.StringBuilder();

        switch (currentPhase)
        {
            case GamePhase.PreMarket:
                if (GamePhaseManager.Inst != null)
                {
                    var orders = GamePhaseManager.Inst.PendingOrders;
                    if (orders.Count > 0)
                    {
                        BuildInteractiveOrderRows(orders);
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
                    var pmOrders = GamePhaseManager.Inst.PendingOrders;
                    if (pmOrders.Count == 0)
                    {
                        sb.AppendLine("<color=#888888>No pending orders.</color>");
                        sb.AppendLine("<color=#888888>Queue orders for tomorrow, or advance day.</color>");
                    }
                }
                break;
        }

        ordersText.text = sb.ToString().TrimEnd();

        if (currentPhase == GamePhase.PostMarket && GamePhaseManager.Inst != null)
        {
            var pmPending = GamePhaseManager.Inst.PendingOrders;
            if (pmPending.Count > 0)
                BuildInteractiveOrderRows(pmPending);
        }
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
        if (string.IsNullOrEmpty(selectedTicker))
        {
            if (TutorialMode) ShowTutCaseyMessage("Pick a stock from the list first.");
            else SetStatus("Select a ticker first.");
            return;
        }
        if (!tickerTradableToday)
        {
            if (TutorialMode) ShowTutCaseyMessage("That stock isn't available right now. Try a different one.");
            else SetStatus("This ticker has no data today (halted/delisted).");
            return;
        }
        if (!int.TryParse(quantityInput.text, out int qty) || qty <= 0)
        {
            if (TutorialMode) ShowTutCaseyMessage("Enter a number of shares first — try 10.");
            else SetStatus("Enter a valid quantity.");
            return;
        }

        if (TutorialMode)
        {
            if (tutorialOrderQueued)
            {
                ShowTutCaseyMessage("We're just practicing with one stock for now. Hit Confirm Trades to see how it executes.");
                return;
            }
            tutorialOrderQueued = true;
            tutorialOrderTicker = selectedTicker;
            tutorialOrderSide = side;
            tutorialOrderQty = qty;
            if (ordersText != null)
                ordersText.text = $"<b>{side.ToUpper()}</b>  {qty}  {selectedTicker}  (Market)";
            double estimatedCost = currentEstimatedPrice * qty;
            if (cashText != null)
                cashText.text = $"Cash: ${(10000.0 - estimatedCost):N2}";
            ShowTutCaseyMessage("Good — now hit Confirm Trades to execute the demo trade.");
            return;
        }

        if (GamePhaseManager.Inst == null) return;

        // DISABLED: limit/stop order price parsing commented out
        double limitPrice = 0;
        double stopPrice = 0;
        // if (selectedOrderType != "market" && priceInput != null)
        // {
        //     if (selectedOrderType == "stop_limit")
        //     {
        //         var parts = priceInput.text.Split(',');
        //         if (parts.Length != 2
        //             || !double.TryParse(parts[0].Trim(), out double sp) || sp <= 0
        //             || !double.TryParse(parts[1].Trim(), out double lp) || lp <= 0)
        //         {
        //             SetStatus("Enter stop,limit prices (e.g. 150,148).");
        //             return;
        //         }
        //         stopPrice = sp;
        //         limitPrice = lp;
        //     }
        //     else
        //     {
        //         if (!double.TryParse(priceInput.text, out double enteredPrice) || enteredPrice <= 0)
        //         {
        //             SetStatus("Enter a valid price.");
        //             return;
        //         }
        //         if (selectedOrderType == "limit") limitPrice = enteredPrice;
        //         else if (selectedOrderType == "stop") stopPrice = enteredPrice;
        //     }
        // }

        double heldShares = 0;
        if (cachedHoldings != null)
            foreach (var h in cachedHoldings)
                if (h.ticker_id == selectedTicker) { heldShares = h.shares_held; break; }

        bool success = GamePhaseManager.Inst.QueueOrder(selectedTicker, side, qty, currentEstimatedPrice, heldShares,
            selectedOrderType, limitPrice, stopPrice);
        if (success)
        {
            string typeLabel = selectedOrderType == "market" ? "" : $" ({selectedOrderType})";
            if (isFirstTradeGuided)
                SetStatus("Casey: Good — now hit Confirm Trades to execute it.");
            else
                SetStatus($"Queued: {side.ToUpper()} {qty} {selectedTicker}{typeLabel}");
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

        string msg2;
        if (isFirstTradeGuided && filled > 0)
        {
            msg2 = "Casey: You own shares now. Check back tomorrow to see how they move.";
            isFirstTradeGuided = false;
        }
        else
        {
            msg2 = filled > 0 ? $"{filled} order(s) filled at close." : "No orders filled.";
            if (failed > 0) msg2 += $" {failed} failed.";
        }
        SetStatus(msg2);

        if (filled > 0) _ = HandleFirstTradeCompletion();

        await RefreshPortfolio();
        if (showingPortfolioChart)
            await LoadPortfolioChart();
        RefreshOrdersDisplay();

        if (!TutorialMode)
            Close();
    }

    private async void OnOpenMarkets()
    {
        if (TutorialMode)
        {
            if (!tutorialOrderQueued)
            {
                ShowTutCaseyMessage("Queue a trade first — pick a stock, set quantity, and hit Buy.");
                return;
            }
            ShowTutCaseyMessage($"Demo trade filled — {tutorialOrderQty} share(s) of {tutorialOrderTicker}! That's how it works. You'll start fresh when real trading begins.");
            tutorialOrderQueued = false;
            TutorialTradeConfirmed = true;
            return;
        }

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
            Debug.LogWarning($"[TradingUI] OpenMarkets returned null. Phase={GamePhaseManager.Inst.CurrentPhase}, IsTransitioning={GamePhaseManager.Inst.IsTransitioning}");
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

        string msg;
        if (isFirstTradeGuided && filled > 0)
        {
            msg = "Casey: You own shares now. Check back after market hours to see how you did.";
            isFirstTradeGuided = false;
        }
        else
        {
            msg = filled > 0 ? $"{filled} order(s) filled." : "Markets open — no orders.";
            if (failed > 0) msg += $" {failed} failed.";
        }
        SetStatus(msg);

        if (filled > 0) _ = HandleFirstTradeCompletion();

        currentPhase = GamePhase.Day;
        dateText.text = FormatDateHeader();
        await RefreshPrice();
        await RefreshPortfolio();
        if (showingPortfolioChart)
            await LoadPortfolioChart();
        ApplyPhaseUI();
        SwitchTab(activeTab);

        if (!TutorialMode)
            Close();
    }

    private void ClearOrderRows()
    {
        if (ordersTextOriginalParent != null && ordersText != null)
            ordersText.transform.SetParent(ordersTextOriginalParent, false);
        foreach (var go in orderRowObjects)
            if (go != null) Destroy(go);
        orderRowObjects.Clear();
        ordersScrollRect = null;
        ordersContentRT = null;
    }

    private ScrollRect ordersScrollRect;
    private RectTransform ordersContentRT;
    private Transform ordersTextOriginalParent;

    private void EnsureOrdersScroll()
    {
        if (ordersScrollRect != null) return;

        Transform parent = ordersContainer != null ? ordersContainer : ordersText?.transform.parent;
        if (parent == null) return;

        if (ordersText != null && ordersTextOriginalParent == null)
            ordersTextOriginalParent = ordersText.transform.parent;

        var viewportGO = new GameObject("OrdersViewport", typeof(RectTransform), typeof(RectMask2D));
        viewportGO.transform.SetParent(parent, false);
        var viewportRT = viewportGO.GetComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.sizeDelta = Vector2.zero;
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = Vector2.zero;

        var contentGO = new GameObject("OrdersContent", typeof(RectTransform));
        contentGO.transform.SetParent(viewportGO.transform, false);
        ordersContentRT = contentGO.GetComponent<RectTransform>();
        ordersContentRT.anchorMin = new Vector2(0, 1);
        ordersContentRT.anchorMax = new Vector2(1, 1);
        ordersContentRT.pivot = new Vector2(0.5f, 1);
        ordersContentRT.anchoredPosition = Vector2.zero;
        ordersContentRT.sizeDelta = new Vector2(0, 0);

        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 2;

        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        ordersScrollRect = viewportGO.AddComponent<ScrollRect>();
        ordersScrollRect.viewport = viewportRT;
        ordersScrollRect.content = ordersContentRT;
        ordersScrollRect.vertical = true;
        ordersScrollRect.horizontal = false;
        ordersScrollRect.movementType = ScrollRect.MovementType.Clamped;
        ordersScrollRect.scrollSensitivity = 20f;

        var scrollbarGO = new GameObject("OrdersScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarGO.transform.SetParent(parent, false);
        var sbRT = scrollbarGO.GetComponent<RectTransform>();
        sbRT.anchorMin = new Vector2(1, 0);
        sbRT.anchorMax = new Vector2(1, 1);
        sbRT.pivot = new Vector2(1, 0.5f);
        sbRT.offsetMin = new Vector2(-6f, 0f);
        sbRT.offsetMax = new Vector2(0f, 0f);
        scrollbarGO.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.3f);

        var handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGO.transform.SetParent(scrollbarGO.transform, false);
        var handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.anchorMin = Vector2.zero;
        handleRT.anchorMax = Vector2.one;
        handleRT.sizeDelta = Vector2.zero;
        handleGO.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.55f, 0.5f);

        var scrollbar = scrollbarGO.GetComponent<Scrollbar>();
        scrollbar.handleRect = handleRT;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleGO.GetComponent<Image>();
        ordersScrollRect.verticalScrollbar = scrollbar;
        ordersScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        orderRowObjects.Add(viewportGO);
        orderRowObjects.Add(scrollbarGO);

        if (ordersText != null)
        {
            ordersText.transform.SetParent(ordersContentRT, false);
            ordersText.transform.SetAsFirstSibling();
            var textLE = ordersText.GetComponent<LayoutElement>();
            if (textLE == null) textLE = ordersText.gameObject.AddComponent<LayoutElement>();
            textLE.flexibleWidth = 1;
        }
    }

    private void BuildInteractiveOrderRows(IReadOnlyList<GamePhaseManager.LocalPendingOrder> orders)
    {
        EnsureOrdersScroll();
        Transform rowParent = ordersContentRT != null ? ordersContentRT : ordersContainer;
        if (rowParent == null) rowParent = ordersText?.transform.parent;
        if (rowParent == null) return;

        for (int i = 0; i < orders.Count; i++)
        {
            var o = orders[i];
            int orderIndex = i;

            var row = new GameObject($"OrderRow_{i}", typeof(RectTransform));
            row.transform.SetParent(rowParent, false);
            var rowRT = row.GetComponent<RectTransform>();
            rowRT.sizeDelta = new Vector2(0, 22);

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = 4;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(2, 2, 0, 0);

            string sideColor = o.side == "buy" ? "#26BF59" : "#D93838";
            string typeTag = o.orderType != "market" ? $" [{o.orderType}]" : "";
            string priceTag = "";
            if (o.limitPrice > 0) priceTag += $" lmt ${FmtPrice(o.limitPrice)}";
            if (o.stopPrice > 0) priceTag += $" stp ${FmtPrice(o.stopPrice)}";

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(row.transform, false);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = $"<color={sideColor}>{o.side.ToUpper()}</color>  {o.quantity} {o.ticker}  ~${FmtPrice(o.estimatedPrice)}{typeTag}{priceTag}";
            tmp.fontSize = 13;
            tmp.enableWordWrapping = false;
            var textLE = textGO.AddComponent<LayoutElement>();
            textLE.flexibleWidth = 1;

            var cancelGO = new GameObject("Cancel", typeof(RectTransform));
            cancelGO.transform.SetParent(row.transform, false);
            var cancelImg = cancelGO.AddComponent<Image>();
            cancelImg.color = new Color(0.85f, 0.22f, 0.22f, 0.8f);
            var cancelBtn = cancelGO.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelImg;
            var cancelLE = cancelGO.AddComponent<LayoutElement>();
            cancelLE.minWidth = 20;
            cancelLE.minHeight = 18;
            cancelLE.preferredWidth = 20;

            var cancelTextGO = new GameObject("X", typeof(RectTransform));
            cancelTextGO.transform.SetParent(cancelGO.transform, false);
            var cancelTmp = cancelTextGO.AddComponent<TextMeshProUGUI>();
            cancelTmp.text = "X";
            cancelTmp.fontSize = 12;
            cancelTmp.alignment = TextAlignmentOptions.Center;
            cancelTmp.color = Color.white;
            cancelTmp.raycastTarget = false;
            var crt = cancelTextGO.GetComponent<RectTransform>();
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.sizeDelta = Vector2.zero;

            cancelBtn.onClick.AddListener(() =>
            {
                if (GamePhaseManager.Inst != null)
                {
                    GamePhaseManager.Inst.RemoveOrder(orderIndex);
                    RefreshOrdersDisplay();
                    RefreshCashDisplay();
                }
            });

            orderRowObjects.Add(row);
        }
    }

    private async Task HandleFirstTradeCompletion()
    {
        try
        {
            if (KnowledgeGraphManager.Inst == null || !KnowledgeGraphManager.Inst.IsInitialized) return;
            if (KnowledgeGraphManager.Inst.IsNodeCompleted("market_buy_sell")) return;

            if (PlayerObjectivesUI.Inst != null)
            {
                PlayerObjectivesUI.Inst.CompleteObjective("tutorial_first_trade");
                await System.Threading.Tasks.Task.Delay(2000);
                PlayerObjectivesUI.Inst.RemoveObjective("tutorial_first_trade");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[TradingUI] First trade completion failed: {e.Message}");
        }
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

    // ── Tutorial Casey Dialogue Panel ──

    private void EnsureTutCaseyPanel()
    {
        if (tutCaseyPanel != null) return;

        tutCaseyPanel = new GameObject("TutCaseyPanel", typeof(RectTransform), typeof(Image));
        tutCaseyPanel.transform.SetParent(tradingPanel.transform, false);
        tutCaseyPanel.transform.SetAsLastSibling();

        var rt = tutCaseyPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, 0.02f);
        rt.anchorMax = new Vector2(0.95f, 0.18f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        tutCaseyPanel.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.1f, 0.95f);

        var nameGO = new GameObject("Name", typeof(RectTransform));
        nameGO.transform.SetParent(tutCaseyPanel.transform, false);
        var nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0f, 1f);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.pivot = new Vector2(0f, 1f);
        nameRT.anchoredPosition = new Vector2(14f, -6f);
        nameRT.sizeDelta = new Vector2(-28f, 22f);
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text = "Casey";
        nameTMP.fontSize = UIConfig.Inst != null ? UIConfig.Inst.Scale(18f) : 18f;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color = new Color(0.4f, 0.85f, 0.7f);
        nameTMP.raycastTarget = false;

        var bodyGO = new GameObject("Body", typeof(RectTransform));
        bodyGO.transform.SetParent(tutCaseyPanel.transform, false);
        var bodyRT = bodyGO.GetComponent<RectTransform>();
        bodyRT.anchorMin = Vector2.zero;
        bodyRT.anchorMax = Vector2.one;
        bodyRT.offsetMin = new Vector2(14f, 8f);
        bodyRT.offsetMax = new Vector2(-14f, -30f);
        tutCaseyBody = bodyGO.AddComponent<TextMeshProUGUI>();
        tutCaseyBody.fontSize = UIConfig.Inst != null ? UIConfig.Inst.Scale(15f) : 15f;
        tutCaseyBody.color = Color.white;
        tutCaseyBody.enableWordWrapping = true;
        tutCaseyBody.overflowMode = TextOverflowModes.Ellipsis;
        tutCaseyBody.raycastTarget = false;

        tutCaseyPanel.SetActive(false);
    }

    private void ShowTutCaseyMessage(string text)
    {
        EnsureTutCaseyPanel();
        tutCaseyPanel.SetActive(true);
        tutCaseyPanel.transform.SetAsLastSibling();
        tutCaseyBody.text = text;
        tutCaseyBody.ForceMeshUpdate();
        tutCaseyTotalChars = tutCaseyBody.textInfo.characterCount;
        tutCaseyBody.maxVisibleCharacters = 0;
        tutCaseyCharAccum = 0f;
        tutCaseyTyping = true;
        StopCoroutine(nameof(TutCaseyTypewriterCo));
        StartCoroutine(TutCaseyTypewriterCo());
    }

    private System.Collections.IEnumerator TutCaseyTypewriterCo()
    {
        while (tutCaseyTyping)
        {
            tutCaseyCharAccum += Time.deltaTime * TutCaseyCharsPerSec;
            int visible = Mathf.Min(tutCaseyTotalChars, (int)tutCaseyCharAccum);
            tutCaseyBody.maxVisibleCharacters = visible;
            if (visible >= tutCaseyTotalChars)
                tutCaseyTyping = false;
            yield return null;
        }
    }

    private void HideTutCaseyPanel()
    {
        if (tutCaseyPanel != null)
            tutCaseyPanel.SetActive(false);
        tutCaseyTyping = false;
    }
}
