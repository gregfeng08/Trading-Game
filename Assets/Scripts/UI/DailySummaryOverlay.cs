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
    private GameObject dividerPositions;
    private TMP_Text positionsHeader;
    private TMP_Text positionsBody;
    private GameObject divider2;
    private TMP_Text moversHeader;
    private TMP_Text moversBody;
    private GameObject dividerPortfolio;
    private TMP_Text portfolioHeader;
    private TMP_Text portfolioBody;
    private GameObject divider3;
    private TMP_Text alertsHeader;
    private TMP_Text alertsBody;
    private GameObject dividerReview;
    private TMP_Text reviewHeader;
    private TMP_Text reviewBody;
    private GameObject dividerCasey;
    private TMP_Text caseyLabel;
    private TMP_Text caseyComment;
    private TMP_Text continueText;
    private GameObject buttonRow;
    private Button continueButton;
    private Button skipWeekButton;

    private bool waitingForInput;
    private bool weekSkipChosen;
    private bool skipRequested;
    private float showStartTime;
    private Coroutine activeSequence;
    private bool showInProgress;
    private Action onDismissed;

    private string caseyLlmLine;
    private bool caseyFetchDone;

    // scrollRect removed — using simple centered content like ArcTransitionOverlay

    // Captured data (snapshot before advance)
    private List<TradeResult> capturedResults;
    private ForcedLiquidationDTO[] capturedLiquidations;
    private MarketMoversResponse capturedMovers;
    private double capturedNetWorth;
    private double capturedPrevNetWorth;
    private PortfolioTotalDTO[] capturedHoldings;
    private readonly Dictionary<string, double> capturedPrevClose = new Dictionary<string, double>();

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    void Update()
    {
        if (!waitingForInput && showInProgress && Input.anyKeyDown
                 && Time.time - showStartTime > fadeToBlackDuration + 0.5f)
            skipRequested = true;
    }

    public bool IsActive => showInProgress || (overlayCanvas != null && overlayCanvas.activeSelf);

    public void Show(Action onComplete = null)
    {
        if (showInProgress || activeSequence != null) return;
        showInProgress = true;
        skipRequested = false;
        showStartTime = Time.time;

        onDismissed = onComplete;

        var gpm = GamePhaseManager.Inst;
        capturedResults = gpm != null ? new List<TradeResult>(gpm.TodayResults) : new();
        capturedLiquidations = gpm?.LastForcedLiquidations;

        string date = gpm?.CurrentDate;
        if (!string.IsNullOrEmpty(date))
            _ = FetchDataAsync(date);
        else
            dataFetched = true;

        _ = FetchCaseyCommentAsync();

        activeSequence = StartCoroutine(SummarySequence());
    }

    private bool dataFetched;

    private async Task FetchDataAsync(string date)
    {
        dataFetched = false;
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

        try
        {
            var portfolioResp = await TradeAPI.GetPortfolio(APIBootstrapper.EntityDbId);
            capturedHoldings = portfolioResp.totals;

            capturedPrevClose.Clear();
            if (capturedHoldings != null)
            {
                foreach (var h in capturedHoldings)
                {
                    try
                    {
                        string startDate = date;
                        if (DateTime.TryParse(date, out var dt))
                            startDate = dt.AddDays(-10).ToString("yyyy-MM-dd");
                        var priceResp = await MarketAPI.GetPrices(h.ticker_id, startDate, date);
                        if (priceResp.rows != null && priceResp.rows.Length >= 2)
                            capturedPrevClose[h.ticker_id] = priceResp.rows[priceResp.rows.Length - 2].close_price;
                    }
                    catch { }
                }
            }
        }
        catch
        {
            capturedHoldings = null;
        }

        dataFetched = true;
    }

    private async Task FetchCaseyCommentAsync()
    {
        caseyLlmLine = null;
        caseyFetchDone = false;
        try
        {
            var resp = await CaseyAPI.GetDailyComment(APIBootstrapper.EntityExternalId);
            if (resp != null && !string.IsNullOrEmpty(resp.comment))
                caseyLlmLine = resp.comment;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DailySummary] Casey comment fetch failed: {ex.Message}");
        }
        caseyFetchDone = true;
    }

    private IEnumerator Stagger()
    {
        if (skipRequested) yield break;
        float t = 0f;
        while (t < staggerDelay && !skipRequested)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    private void RevealAll()
    {
        if (capturedResults != null && capturedResults.Count > 0)
        {
            divider1.SetActive(true);
            tradesHeader.gameObject.SetActive(true);
            tradesBody.gameObject.SetActive(true);
            pnlText.gameObject.SetActive(true);
        }
        if (capturedHoldings != null && capturedHoldings.Length > 0)
        {
            dividerPositions.SetActive(true);
            positionsHeader.gameObject.SetActive(true);
            positionsBody.gameObject.SetActive(true);
        }
        if (capturedMovers != null)
        {
            divider2.SetActive(true);
            moversHeader.gameObject.SetActive(true);
            moversBody.gameObject.SetActive(true);
        }
        if (capturedNetWorth > 0)
        {
            dividerPortfolio.SetActive(true);
            portfolioHeader.gameObject.SetActive(true);
            portfolioBody.gameObject.SetActive(true);
        }
        bool hasAlerts = !string.IsNullOrEmpty(alertsHeader.text);
        divider3.SetActive(hasAlerts);
        alertsHeader.gameObject.SetActive(hasAlerts);
        alertsBody.gameObject.SetActive(!string.IsNullOrEmpty(alertsBody.text));
        if (reviewHeader != null && reviewHeader.text.Length > 0)
        {
            dividerReview.SetActive(true);
            reviewHeader.gameObject.SetActive(true);
            reviewBody.gameObject.SetActive(true);
        }
        if (caseyComment != null && !string.IsNullOrEmpty(caseyComment.text))
        {
            dividerCasey.SetActive(true);
            caseyLabel.gameObject.SetActive(true);
            caseyComment.gameObject.SetActive(true);
        }
    }

    private IEnumerator SummarySequence()
    {
        overlayCanvas.SetActive(true);
        LockPlayer();
        HideAll();
        canvasGroup.blocksRaycasts = true;

        yield return Fade(0f, 1f, fadeToBlackDuration);

        // Show header immediately
        string date = GamePhaseManager.Inst?.CurrentDate ?? "---";
        SetText(sleepLabel, "END OF DAY", dimColor);
        SetText(dateHeader, FormatDate(date), headerColor);



        // Wait for data to arrive (with a timeout)
        float waitTime = 0f;
        while (!dataFetched && waitTime < 5f)
        {
            waitTime += Time.deltaTime;
            yield return null;
        }

        yield return Stagger();

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
            yield return Stagger();

            string totalColor = totalPnl >= 0 ? ColorHex(gainColor) : ColorHex(lossColor);
            string totalSign = totalPnl >= 0 ? "+" : "";
            SetText(pnlText, $"Day P&L:  <color={totalColor}>{totalSign}${TradingUIController.FmtPrice(totalPnl)}</color>", bodyColor);
            yield return Stagger();
        }

        // Positions section
        if (capturedHoldings != null && capturedHoldings.Length > 0)
        {
            dividerPositions.SetActive(true);
            SetText(positionsHeader, "POSITIONS", dimColor);

            var sb = new System.Text.StringBuilder();
            double totalPosPnl = 0;
            foreach (var h in capturedHoldings)
            {
                capturedPrevClose.TryGetValue(h.ticker_id, out double prevClose);
                double dayChange = prevClose > 0 ? h.current_price - prevClose : 0;
                double dayChangePct = prevClose > 0 ? (dayChange / prevClose) * 100 : 0;
                double posDayPnl = dayChange * h.shares_held;
                totalPosPnl += posDayPnl;

                string arrow = dayChange >= 0 ? "▲" : "▼";
                string changeColor = dayChange >= 0 ? ColorHex(gainColor) : ColorHex(lossColor);
                string sign = dayChange >= 0 ? "+" : "";

                sb.AppendLine($"<b>{h.ticker_id}</b>  {h.shares_held:F0} shares  ${TradingUIController.FmtPrice(h.current_price)}");
                if (prevClose > 0)
                    sb.AppendLine($"  <color={changeColor}>{arrow} {sign}{dayChangePct:F1}%  {sign}${TradingUIController.FmtPrice(posDayPnl)}</color>");
            }

            string ptColor = totalPosPnl >= 0 ? ColorHex(gainColor) : ColorHex(lossColor);
            string ptSign = totalPosPnl >= 0 ? "+" : "";
            sb.AppendLine($"\nPositions P&L:  <color={ptColor}>{ptSign}${TradingUIController.FmtPrice(totalPosPnl)}</color>");

            SetText(positionsBody, sb.ToString().TrimEnd(), bodyColor);
            yield return Stagger();
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
                yield return Stagger();
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
            yield return Stagger();
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
                yield return Stagger();
            }
        }

        // To Review section
        try
        {
            var reviewSb = new System.Text.StringBuilder();

            if (KnowledgeGraphManager.Inst?.Nodes != null)
            {
                int pending = 0;
                foreach (var n in KnowledgeGraphManager.Inst.Nodes)
                {
                    if (n.status == "unlocked")
                        pending++;
                }
                if (pending > 0)
                    reviewSb.AppendLine($"<color={ColorHex(new Color(0.9f, 0.75f, 0.2f))}>{pending} insight(s) available in the Knowledge Graph</color>");
            }

            if (capturedHoldings != null && capturedNetWorth > 0)
            {
                foreach (var h in capturedHoldings)
                {
                    double pct = h.market_value / capturedNetWorth * 100;
                    if (pct > 50)
                        reviewSb.AppendLine($"<color={ColorHex(new Color(0.9f, 0.6f, 0.2f))}>{h.ticker_id} is {pct:F0}% of your portfolio — consider diversifying</color>");
                }
            }

            if (reviewSb.Length > 0)
            {
                dividerReview.SetActive(true);
                SetText(reviewHeader, "TO REVIEW", dimColor);
                SetText(reviewBody, reviewSb.ToString().TrimEnd(), bodyColor);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[DailySummary] Review section failed: {ex.Message}");
        }
        yield return Stagger();

        // Casey's closing thought — wait for LLM or timeout to static fallback
        {
            dividerCasey.SetActive(true);
            SetText(caseyLabel, "CASEY", new Color(0.55f, 0.78f, 0.65f, 1f));

            if (!caseyFetchDone)
            {
                SetText(caseyComment, "Casey is thinking...", dimColor);
                float caseyWait = 0f;
                while (!caseyFetchDone && caseyWait < 12f)
                {
                    caseyWait += Time.deltaTime;
                    yield return null;
                }
            }

            string caseyLine = !string.IsNullOrEmpty(caseyLlmLine) ? caseyLlmLine : PickCaseyLine();
            SetText(caseyComment, $"\"{caseyLine}\"", new Color(0.82f, 0.85f, 0.80f, 1f));
            yield return Stagger();
        }

        if (skipRequested) RevealAll();

        buttonRow.SetActive(true);
        weekSkipChosen = false;
        skipRequested = false;
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

        if (weekSkipChosen)
        {
            weekSkipChosen = false;
            onDismissed = null;
            _ = RunWeekSkip();
        }
        else
        {
            var callback = onDismissed;
            onDismissed = null;
            callback?.Invoke();
        }
    }

    private async Task RunWeekSkip()
    {
        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.CUTSCENE);

        try
        {
            var gpm = GamePhaseManager.Inst;
            if (gpm == null) return;

            var resp = await gpm.AdvanceWeek(5);
            if (resp != null && WeekReviewOverlay.Inst != null)
            {
                WeekReviewOverlay.Inst.Show(resp, () =>
                {
                    if (PlayerStateController.Inst != null)
                        PlayerStateController.Inst.SetState(PlayerState.MOVING);
                });
                return;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[DailySummary] Week skip failed: {e.Message}");
        }

        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.MOVING);
    }

    private void OnContinueClicked()
    {
        weekSkipChosen = false;
        waitingForInput = false;
    }

    private void OnSkipWeekClicked()
    {
        weekSkipChosen = true;
        waitingForInput = false;
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
        dividerPositions.SetActive(false);
        positionsHeader.gameObject.SetActive(false);
        positionsBody.gameObject.SetActive(false);
        divider2.SetActive(false);
        moversHeader.gameObject.SetActive(false);
        moversBody.gameObject.SetActive(false);
        dividerPortfolio.SetActive(false);
        portfolioHeader.gameObject.SetActive(false);
        portfolioBody.gameObject.SetActive(false);
        divider3.SetActive(false);
        alertsHeader.gameObject.SetActive(false);
        alertsBody.gameObject.SetActive(false);
        if (dividerReview != null) dividerReview.SetActive(false);
        if (reviewHeader != null) reviewHeader.gameObject.SetActive(false);
        if (reviewBody != null) reviewBody.gameObject.SetActive(false);
        if (dividerCasey != null) dividerCasey.SetActive(false);
        if (caseyLabel != null) caseyLabel.gameObject.SetActive(false);
        if (caseyComment != null) caseyComment.gameObject.SetActive(false);
        continueText.gameObject.SetActive(false);
        if (buttonRow != null) buttonRow.SetActive(false);
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

    // ── Casey's Daily Comment ──

    private string PickCaseyLine()
    {
        bool traded = capturedResults != null && capturedResults.Count > 0;
        double dayChange = capturedNetWorth - capturedPrevNetWorth;
        double dayChangePct = capturedPrevNetWorth > 0 ? (dayChange / capturedPrevNetWorth) * 100.0 : 0;

        double maxConcentration = 0;
        string concentratedTicker = null;
        if (capturedHoldings != null && capturedNetWorth > 0)
        {
            foreach (var h in capturedHoldings)
            {
                double pct = h.market_value / capturedNetWorth;
                if (pct > maxConcentration)
                {
                    maxConcentration = pct;
                    concentratedTicker = h.ticker_id;
                }
            }
        }

        double cashRatio = 0;
        var gpm = GamePhaseManager.Inst;
        if (gpm != null && capturedNetWorth > 0)
        {
            double cash = capturedNetWorth - TotalHoldingsValue();
            cashRatio = cash / capturedNetWorth;
        }

        bool hasUnlockedNodes = false;
        if (KnowledgeGraphManager.Inst?.Nodes != null)
        {
            foreach (var n in KnowledgeGraphManager.Inst.Nodes)
                if (n.status == "unlocked") { hasUnlockedNodes = true; break; }
        }

        string arcName = gpm?.ArcName;
        bool isMeltdown = arcName != null && arcName.Contains("Meltdown");
        bool isUnraveling = arcName != null && arcName.Contains("Unraveling");

        // Priority-ordered conditions — first match wins
        if (dayChangePct <= -5.0)
            return Pick(bigLossLines);
        if (dayChangePct >= 5.0)
            return Pick(bigGainLines);
        if (maxConcentration > 0.7 && traded)
            return Pick(concentrationLines);
        if (cashRatio < 0.1 && capturedHoldings != null && capturedHoldings.Length > 0)
            return Pick(lowCashLines);
        if (hasUnlockedNodes)
            return Pick(newInsightLines);
        if (!traded && isMeltdown)
            return Pick(meltdownQuietLines);
        if (!traded && isUnraveling)
            return Pick(unravelingQuietLines);
        if (!traded)
            return Pick(noTradeLines);
        if (dayChangePct < -1.0)
            return Pick(mildLossLines);
        if (dayChangePct > 1.0)
            return Pick(mildGainLines);

        return Pick(neutralLines);
    }

    private double TotalHoldingsValue()
    {
        if (capturedHoldings == null) return 0;
        double total = 0;
        foreach (var h in capturedHoldings) total += h.market_value;
        return total;
    }

    private static string Pick(string[] pool)
    {
        return pool[UnityEngine.Random.Range(0, pool.Length)];
    }

    private static readonly string[] bigLossLines = {
        "Rough one. Take a breath. One day doesn't define your whole run.",
        "Days like this are where the learning actually happens. Doesn't make it fun though.",
        "The market took a bite today. The question is what you do tomorrow, not what happened today.",
        "Yeah. That hurts. But you're still in the game — that counts for something."
    };

    private static readonly string[] bigGainLines = {
        "Good day on paper. Just remember — the market gives before it takes.",
        "Nice numbers. Don't let one green day convince you you've figured it all out.",
        "Solid. Now the hard part: not chasing that feeling tomorrow.",
        "Strong day. Enjoy it, but don't mistake luck for skill just yet."
    };

    private static readonly string[] concentrationLines = {
        "You're leaning hard into one name. That's a bet, not a portfolio.",
        "Just flagging — most of your eggs are in one basket right now.",
        "High conviction is fine. Just make sure it's conviction and not inertia.",
        "You've got a lot riding on one ticker. Make sure that's deliberate."
    };

    private static readonly string[] lowCashLines = {
        "You're running thin on cash. If something moves against you, there's no cushion.",
        "Low cash means low options. Something to think about.",
        "Not a lot of dry powder left. If an opportunity shows up tomorrow, can you take it?",
        "Just noting — you're nearly fully deployed. That's fine until it isn't."
    };

    private static readonly string[] newInsightLines = {
        "New insight unlocked. Might be worth checking the knowledge graph before tomorrow.",
        "The lattice picked something up from your trading today. Take a look when you get a chance.",
        "Something clicked today — the graph's got a new node for you.",
        "You triggered a new lesson. That means you did something worth learning from."
    };

    private static readonly string[] noTradeLines = {
        "Quiet day. Sometimes watching is the move.",
        "No trades today. Nothing wrong with observing.",
        "Sat this one out. That's a valid strategy too.",
        "Rest day. The market will still be there tomorrow."
    };

    private static readonly string[] meltdownQuietLines = {
        "Smart to stay quiet when everything's on fire.",
        "Not trading in a panic is harder than it sounds. Good discipline.",
        "Sometimes survival means doing nothing. Today was one of those days.",
        "Sitting on your hands during chaos takes more guts than people think."
    };

    private static readonly string[] unravelingQuietLines = {
        "Hard to know what to do when the signals are mixed. Watching is fair.",
        "Nobody knows where the floor is right now. Caution isn't cowardice.",
        "The uncertainty gets to everyone. No shame in stepping back.",
        "Tricky market. Sometimes the best trade is the one you don't make."
    };

    private static readonly string[] mildLossLines = {
        "Slightly down. Comes with the territory.",
        "Small red day. Not every day's a winner — the question is the trend.",
        "A little in the red. Nothing to panic about, but worth a check on your thesis.",
        "Minor setback. Keep your eye on the bigger picture."
    };

    private static readonly string[] mildGainLines = {
        "Modest green day. Steady works.",
        "Small win. Those add up if you stay consistent.",
        "In the green. Nothing flashy, but that's usually how good trading looks.",
        "Positive day. Boring is fine — boring compounds."
    };

    private static readonly string[] neutralLines = {
        "Flat day. The market's thinking. You should be too.",
        "Not much movement. Good time to review your positions.",
        "Sideways. These days feel pointless, but they're where plans get made.",
        "Nothing dramatic. Use the quiet to think about what's next."
    };

    // ── UI Construction (matches ArcTransitionOverlay pattern) ──

    private void BuildUI()
    {
        var canvasGO = new GameObject("DailySummaryCanvas");
        overlayCanvas = canvasGO;
        canvasGO.transform.SetParent(transform);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

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
        bg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 1f);

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
        dividerPositions = MakeDivider();
        positionsHeader = MakeText("PositionsHeader", 16, FontStyles.Bold, TextAlignmentOptions.Center);
        positionsBody = MakeText("PositionsBody", 16, FontStyles.Normal, TextAlignmentOptions.Center);
        divider2     = MakeDivider();
        moversHeader = MakeText("MoversHeader", 16, FontStyles.Bold, TextAlignmentOptions.Center);
        moversBody   = MakeText("MoversBody", 16, FontStyles.Normal, TextAlignmentOptions.Center);
        dividerPortfolio = MakeDivider();
        portfolioHeader  = MakeText("PortfolioHeader", 16, FontStyles.Bold, TextAlignmentOptions.Center);
        portfolioBody    = MakeText("PortfolioBody", 16, FontStyles.Normal, TextAlignmentOptions.Center);
        divider3     = MakeDivider();
        alertsHeader = MakeText("AlertsHeader", 16, FontStyles.Bold, TextAlignmentOptions.Center);
        alertsBody   = MakeText("AlertsBody", 16, FontStyles.Normal, TextAlignmentOptions.Center);
        dividerReview = MakeDivider();
        reviewHeader = MakeText("ReviewHeader", 16, FontStyles.Bold, TextAlignmentOptions.Center);
        reviewBody   = MakeText("ReviewBody", 16, FontStyles.Normal, TextAlignmentOptions.Center);
        dividerCasey = MakeDivider();
        caseyLabel   = MakeText("CaseyLabel", 14, FontStyles.Bold, TextAlignmentOptions.Center);
        caseyComment = MakeText("CaseyComment", 17, FontStyles.Italic, TextAlignmentOptions.Center);
        MakeSpacer(20);
        continueText = MakeText("ContinueLabel", 14, FontStyles.Normal, TextAlignmentOptions.Center);

        buttonRow = new GameObject("ButtonRow");
        buttonRow.transform.SetParent(contentRoot.transform, false);
        buttonRow.AddComponent<RectTransform>();
        var hlg = buttonRow.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 30;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        var rowLE = buttonRow.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 44;

        continueButton = MakeSummaryButton(buttonRow.transform, "Continue", new Color(0.18f, 0.32f, 0.18f), OnContinueClicked);
        skipWeekButton = MakeSummaryButton(buttonRow.transform, "Skip Week", new Color(0.22f, 0.22f, 0.35f), OnSkipWeekClicked);

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

    private Button MakeSummaryButton(Transform parent, string label, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label + "Btn");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(160, 38);
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 16;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.85f, 0.85f, 0.9f);
        tmp.raycastTarget = false;

        return btn;
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
