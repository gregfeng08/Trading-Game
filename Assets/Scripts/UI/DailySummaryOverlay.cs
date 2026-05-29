using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.API;
using Game.API.DTO;

public class DailySummaryOverlay : MonoBehaviour
{
    public static DailySummaryOverlay Inst { get; private set; }

    [Header("Timing")]
    [SerializeField] private float fadeToBlackDuration = 1.0f;
    [SerializeField] private float pauseBeforeText = 0.6f;
    [SerializeField] private float staggerDelay = 0.5f;
    [SerializeField] private float fadeOutDuration = 0.8f;

    [Header("Colors")]
    [SerializeField] private Color headerColor = new(0.85f, 0.75f, 0.45f, 1f);
    [SerializeField] private Color bodyColor = new(0.72f, 0.72f, 0.76f, 1f);
    [SerializeField] private Color dimColor = new(0.4f, 0.4f, 0.45f, 1f);
    [SerializeField] private Color gainColor = new(0.4f, 0.9f, 0.4f, 1f);
    [SerializeField] private Color lossColor = new(0.9f, 0.4f, 0.4f, 1f);
    [SerializeField] private Color alertColor = new(0.9f, 0.6f, 0.2f, 1f);

    private GameObject overlayCanvas;
    private CanvasGroup canvasGroup;
    private GameObject contentRoot;

    private TMP_Text dateHeader;
    private TMP_Text sleepLabel;
    private GameObject divider1;
    private TMP_Text tradesHeader;
    private TMP_Text tradesBody;
    private TMP_Text pnlText;
    private GameObject divider2;
    private TMP_Text moversHeader;
    private TMP_Text moversBody;
    private GameObject dividerPortfolio;
    private TMP_Text portfolioHeader;
    private TMP_Text portfolioBody;
    private GameObject divider3;
    private TMP_Text alertsHeader;
    private TMP_Text alertsBody;
    private TMP_Text continueText;

    private bool waitingForInput;
    private Coroutine activeSequence;
    private bool showInProgress;
    private Action onDismissed;

    // Captured data (snapshot before advance)
    private List<TradeResult> capturedResults;
    private ForcedLiquidationDTO[] capturedLiquidations;
    private MarketMoversResponse capturedMovers;
    private double capturedNetWorth;
    private double capturedPrevNetWorth;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    void Update()
    {
        if (waitingForInput && Input.anyKeyDown)
            waitingForInput = false;
    }

    public bool IsActive => showInProgress || (overlayCanvas != null && overlayCanvas.activeSelf);

    public void Show(Action onComplete = null)
    {
        if (showInProgress || activeSequence != null) return;
        showInProgress = true;

        onDismissed = onComplete;

        // Snapshot today's data before it gets cleared
        var gpm = GamePhaseManager.Inst;
        capturedResults = gpm != null ? new List<TradeResult>(gpm.TodayResults) : new();
        capturedLiquidations = gpm?.LastForcedLiquidations;

        string date = gpm?.CurrentDate;
        if (!string.IsNullOrEmpty(date))
            _ = FetchMoversAndStart(date);
        else
            activeSequence = StartCoroutine(SummarySequence());
    }

    private async Task FetchMoversAndStart(string date)
    {
        try
        {
            capturedMovers = await MarketAPI.GetMarketMovers(date);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DailySummary] Failed to fetch movers: {ex.Message}");
            capturedMovers = null;
        }

        try
        {
            var histResp = await OrderAPI.GetPortfolioHistory(APIBootstrapper.EntityDbId);
            if (histResp.history != null && histResp.history.Length >= 2)
            {
                capturedNetWorth = histResp.history[histResp.history.Length - 1].net_worth;
                capturedPrevNetWorth = histResp.history[histResp.history.Length - 2].net_worth;
            }
            else if (histResp.history != null && histResp.history.Length == 1)
            {
                capturedNetWorth = histResp.history[0].net_worth;
                capturedPrevNetWorth = capturedNetWorth;
            }
            else
            {
                capturedNetWorth = 0;
                capturedPrevNetWorth = 0;
            }
        }
        catch
        {
            capturedNetWorth = 0;
            capturedPrevNetWorth = 0;
        }

        activeSequence = StartCoroutine(SummarySequence());
    }

    private IEnumerator SummarySequence()
    {
        overlayCanvas.SetActive(true);
        LockPlayer();
        HideAll();
        canvasGroup.blocksRaycasts = true;

        // Fade to black
        yield return Fade(0f, 1f, fadeToBlackDuration);

        yield return new WaitForSeconds(pauseBeforeText);

        // Date header
        string date = GamePhaseManager.Inst?.CurrentDate ?? "---";
        SetText(sleepLabel, "END OF DAY", dimColor);
        SetText(dateHeader, FormatDate(date), headerColor);
        yield return new WaitForSeconds(staggerDelay);

        // Trades section
        if (capturedResults != null && capturedResults.Count > 0)
        {
            divider1.SetActive(true);
            SetText(tradesHeader, "YOUR TRADES", dimColor);

            var sb = new System.Text.StringBuilder();
            double totalPnl = 0;

            foreach (var r in capturedResults)
            {
                if (r.status != "ok") continue;

                string side = r.side.ToUpper();
                string sideColor = r.side == "buy" ? ColorHex(gainColor) : ColorHex(lossColor);
                sb.AppendLine($"<color={sideColor}>{side}</color>  {r.quantity} {r.ticker}  @  ${TradingUIController.FmtPrice(r.fillPrice)}");

                if (r.closePrice > 0)
                {
                    double pnl = r.pnl;
                    totalPnl += pnl;
                    string pnlColor = pnl >= 0 ? ColorHex(gainColor) : ColorHex(lossColor);
                    string sign = pnl >= 0 ? "+" : "";
                    sb.AppendLine($"    Close: ${TradingUIController.FmtPrice(r.closePrice)}  <color={pnlColor}>{sign}${TradingUIController.FmtPrice(pnl)}</color>");
                }
            }

            SetText(tradesBody, sb.ToString().TrimEnd(), bodyColor);
            yield return new WaitForSeconds(staggerDelay);

            string totalColor = totalPnl >= 0 ? ColorHex(gainColor) : ColorHex(lossColor);
            string totalSign = totalPnl >= 0 ? "+" : "";
            SetText(pnlText, $"Day P&L:  <color={totalColor}>{totalSign}${TradingUIController.FmtPrice(totalPnl)}</color>", bodyColor);
            yield return new WaitForSeconds(staggerDelay);
        }

        // Market movers
        if (capturedMovers != null)
        {
            bool hasGainers = capturedMovers.gainers != null && capturedMovers.gainers.Length > 0;
            bool hasLosers = capturedMovers.losers != null && capturedMovers.losers.Length > 0;

            if (hasGainers || hasLosers)
            {
                divider2.SetActive(true);
                SetText(moversHeader, "MARKET MOVERS", dimColor);

                var sb = new System.Text.StringBuilder();

                if (hasGainers)
                {
                    foreach (var g in capturedMovers.gainers)
                        sb.AppendLine($"<color={ColorHex(gainColor)}>▲ {g.ticker}  +{g.change_pct:F1}%</color>    ${TradingUIController.FmtPrice(g.close)}");
                }

                if (hasLosers)
                {
                    foreach (var l in capturedMovers.losers)
                        sb.AppendLine($"<color={ColorHex(lossColor)}>▼ {l.ticker}  {l.change_pct:F1}%</color>    ${TradingUIController.FmtPrice(l.close)}");
                }

                SetText(moversBody, sb.ToString().TrimEnd(), bodyColor);
                yield return new WaitForSeconds(staggerDelay);
            }
        }

        // Portfolio performance
        if (capturedNetWorth > 0)
        {
            dividerPortfolio.SetActive(true);
            SetText(portfolioHeader, "PORTFOLIO", dimColor);

            double change = capturedNetWorth - capturedPrevNetWorth;
            double changePct = capturedPrevNetWorth > 0 ? (change / capturedPrevNetWorth) * 100.0 : 0;
            string changeColor = change >= 0 ? ColorHex(gainColor) : ColorHex(lossColor);
            string sign = change >= 0 ? "+" : "";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Net Worth:  ${TradingUIController.FmtPrice(capturedNetWorth)}");
            sb.Append($"Daily Change:  <color={changeColor}>{sign}${TradingUIController.FmtPrice(change)}  ({sign}{changePct:F1}%)</color>");

            SetText(portfolioBody, sb.ToString(), bodyColor);
            yield return new WaitForSeconds(staggerDelay);
        }

        // Alerts (delistings, forced liquidations)
        {
            var sb = new System.Text.StringBuilder();

            if (capturedMovers?.delisted != null && capturedMovers.delisted.Length > 0)
            {
                foreach (var ticker in capturedMovers.delisted)
                    sb.AppendLine($"<color={ColorHex(alertColor)}>⚠ {ticker} DELISTED</color>");
            }

            if (capturedLiquidations != null && capturedLiquidations.Length > 0)
            {
                foreach (var liq in capturedLiquidations)
                    sb.AppendLine($"<color={ColorHex(alertColor)}>⚠ {liq.ticker_id} liquidated — {liq.shares:F0} shares @ ${TradingUIController.FmtPrice(liq.price)}</color>");
            }

            if (sb.Length > 0)
            {
                divider3.SetActive(true);
                SetText(alertsHeader, "ALERTS", dimColor);
                SetText(alertsBody, sb.ToString().TrimEnd(), bodyColor);
                yield return new WaitForSeconds(staggerDelay);
            }
        }

        // Continue prompt
        SetText(continueText, "Press any key to continue", dimColor);
        waitingForInput = true;
        yield return new WaitUntil(() => !waitingForInput);

        yield return Fade(1f, 0f, fadeOutDuration);
        Cleanup();
    }

    private void Cleanup()
    {
        canvasGroup.blocksRaycasts = false;
        HideAll();
        overlayCanvas.SetActive(false);
        activeSequence = null;
        showInProgress = false;

        UnlockPlayer();

        var callback = onDismissed;
        onDismissed = null;
        callback?.Invoke();
    }

    private void LockPlayer()
    {
        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.CUTSCENE);
    }

    private void UnlockPlayer()
    {
        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.MOVING);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    private void HideAll()
    {
        dateHeader.gameObject.SetActive(false);
        sleepLabel.gameObject.SetActive(false);
        divider1.SetActive(false);
        tradesHeader.gameObject.SetActive(false);
        tradesBody.gameObject.SetActive(false);
        pnlText.gameObject.SetActive(false);
        divider2.SetActive(false);
        moversHeader.gameObject.SetActive(false);
        moversBody.gameObject.SetActive(false);
        dividerPortfolio.SetActive(false);
        portfolioHeader.gameObject.SetActive(false);
        portfolioBody.gameObject.SetActive(false);
        divider3.SetActive(false);
        alertsHeader.gameObject.SetActive(false);
        alertsBody.gameObject.SetActive(false);
        continueText.gameObject.SetActive(false);
    }

    private static void SetText(TMP_Text el, string text, Color color)
    {
        el.text = text;
        el.color = color;
        el.gameObject.SetActive(true);
    }

    private static string ColorHex(Color c)
    {
        return $"#{ColorUtility.ToHtmlStringRGB(c)}";
    }

    private static string FormatDate(string isoDate)
    {
        if (string.IsNullOrEmpty(isoDate)) return "---";
        if (System.DateTime.TryParse(isoDate, out var dt))
            return dt.ToString("MMMM d, yyyy");
        return isoDate;
    }

    // ── UI Construction (matches ArcTransitionOverlay pattern) ──

    private void BuildUI()
    {
        var canvasGO = new GameObject("DailySummaryCanvas");
        overlayCanvas = canvasGO;
        canvasGO.transform.SetParent(transform);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 99;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        canvasGroup = canvasGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        var bg = new GameObject("Background");
        bg.transform.SetParent(canvasGO.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        Stretch(bgRect);
        bg.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.04f, 0.96f);

        contentRoot = new GameObject("Content");
        contentRoot.transform.SetParent(bg.transform, false);
        var contentRect = contentRoot.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(700, 0);
        contentRect.anchoredPosition = Vector2.zero;

        var vlg = contentRoot.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 10;
        vlg.padding = new RectOffset(40, 40, 20, 20);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        var csf = contentRoot.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sleepLabel   = MakeText("SleepLabel", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        dateHeader   = MakeText("DateHeader", 32, FontStyles.Bold, TextAlignmentOptions.Center);
        MakeSpacer(8);
        divider1     = MakeDivider();
        tradesHeader = MakeText("TradesHeader", 16, FontStyles.Bold, TextAlignmentOptions.Center);
        tradesBody   = MakeText("TradesBody", 16, FontStyles.Normal, TextAlignmentOptions.Center);
        pnlText      = MakeText("PnL", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        divider2     = MakeDivider();
        moversHeader = MakeText("MoversHeader", 16, FontStyles.Bold, TextAlignmentOptions.Center);
        moversBody   = MakeText("MoversBody", 16, FontStyles.Normal, TextAlignmentOptions.Center);
        dividerPortfolio = MakeDivider();
        portfolioHeader  = MakeText("PortfolioHeader", 16, FontStyles.Bold, TextAlignmentOptions.Center);
        portfolioBody    = MakeText("PortfolioBody", 16, FontStyles.Normal, TextAlignmentOptions.Center);
        divider3     = MakeDivider();
        alertsHeader = MakeText("AlertsHeader", 16, FontStyles.Bold, TextAlignmentOptions.Center);
        alertsBody   = MakeText("AlertsBody", 16, FontStyles.Normal, TextAlignmentOptions.Center);
        MakeSpacer(20);
        continueText = MakeText("Continue", 14, FontStyles.Normal, TextAlignmentOptions.Center);

        HideAll();
        overlayCanvas.SetActive(false);
    }

    private TMP_Text MakeText(string name, float size, FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(contentRoot.transform, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.richText = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    private GameObject MakeDivider()
    {
        var go = new GameObject("Divider");
        go.transform.SetParent(contentRoot.transform, false);
        go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 1;
        le.flexibleWidth = 1;
        return go;
    }

    private void MakeSpacer(float height)
    {
        var go = new GameObject("Spacer");
        go.transform.SetParent(contentRoot.transform, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }
}
