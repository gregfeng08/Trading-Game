using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using Game.API.DTO;

public class WeekReviewOverlay : MonoBehaviour
{
    public static WeekReviewOverlay Inst { get; private set; }

    private GameObject overlayCanvas;
    private CanvasGroup canvasGroup;
    private TMP_Text headerText;
    private TMP_Text summaryText;
    private TMP_Text continueText;

    private bool waitingForInput;
    private Action onDismissed;

    public bool IsActive => overlayCanvas != null && overlayCanvas.activeSelf;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    public void Show(WeekReviewResponse data, Action onComplete)
    {
        if (data == null) { onComplete?.Invoke(); return; }
        onDismissed = onComplete;
        PopulateContent(data);
        overlayCanvas.SetActive(true);
        StartCoroutine(FadeInSequence());
    }

    private const string GainHex = "#66E666";
    private const string LossHex = "#E66666";
    private const string DimHex = "#666673";
    private const string AccentHex = "#D9BF73";

    private void PopulateContent(WeekReviewResponse data)
    {
        headerText.text = $"<color={DimHex}>WEEK IN REVIEW</color>\n<size=28><color={AccentHex}>{FormatDate(data.start_date)}  →  {FormatDate(data.end_date)}</color></size>\n<size=16><color={DimHex}>{data.days_advanced} trading days</color></size>";

        var sb = new System.Text.StringBuilder();

        if (data.daily_summaries != null && data.daily_summaries.Length > 0)
        {
            double startNw = data.daily_summaries[0].net_worth;
            double endNw = data.daily_summaries[^1].net_worth;
            double weekChange = endNw - startNw;
            double weekPct = startNw > 0 ? weekChange / startNw * 100 : 0;
            string sign = weekChange >= 0 ? "+" : "";
            string clr = weekChange >= 0 ? GainHex : LossHex;

            sb.AppendLine($"<size=22><b>Portfolio: ${endNw:N2}</b>  <color={clr}>{sign}${weekChange:N2} ({sign}{weekPct:F1}%)</color></size>");
            sb.AppendLine();

            var holdingTotals = new System.Collections.Generic.Dictionary<string, double>();
            foreach (var day in data.daily_summaries)
            {
                if (day.top_movers == null) continue;
                foreach (var m in day.top_movers)
                {
                    if (!holdingTotals.ContainsKey(m.ticker))
                        holdingTotals[m.ticker] = 0;
                    holdingTotals[m.ticker] += m.change_pct;
                }
            }

            if (holdingTotals.Count > 0)
            {
                sb.AppendLine($"<color={DimHex}>─────────────────────────────────</color>");
                sb.AppendLine($"<color={DimHex}>YOUR HOLDINGS THIS WEEK</color>");
                sb.AppendLine();

                var sorted = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, double>>(holdingTotals);
                sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

                foreach (var kvp in sorted)
                {
                    string hSign = kvp.Value >= 0 ? "+" : "";
                    string hClr = kvp.Value >= 0 ? GainHex : LossHex;
                    sb.AppendLine($"  {kvp.Key,-6} <color={hClr}>{hSign}{kvp.Value:F1}% cumulative</color>");
                }
                sb.AppendLine();
            }

            sb.AppendLine($"<color={DimHex}>─────────────────────────────────</color>");
            sb.AppendLine($"<color={DimHex}>DAILY BREAKDOWN</color>");
            sb.AppendLine();

            double prevNw = startNw;
            foreach (var day in data.daily_summaries)
            {
                double dayChange = day.net_worth - prevNw;
                string dSign = dayChange >= 0 ? "+" : "";
                string dClr = dayChange >= 0 ? GainHex : LossHex;

                sb.Append($"  {FormatDateShort(day.date)}  ${day.net_worth:N2}  <color={dClr}>{dSign}${dayChange:N2}</color>");

                if (day.top_movers != null && day.top_movers.Length > 0)
                {
                    sb.Append("   ");
                    int shown = 0;
                    foreach (var m in day.top_movers)
                    {
                        if (shown >= 3) break;
                        string mSign = m.change_pct >= 0 ? "+" : "";
                        string mClr = m.change_pct >= 0 ? GainHex : LossHex;
                        sb.Append($"<color={mClr}>{m.ticker} {mSign}{m.change_pct:F1}%</color>  ");
                        shown++;
                    }
                }
                sb.AppendLine();
                prevNw = day.net_worth;
            }
        }

        if (data.unlocked_nodes != null && data.unlocked_nodes.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"<color={DimHex}>─────────────────────────────────</color>");
            sb.AppendLine($"<b><color={AccentHex}>NEW CONCEPTS UNLOCKED ({data.unlocked_nodes.Length})</color></b>");
            foreach (var node in data.unlocked_nodes)
                sb.AppendLine($"  • {node.title}");
        }

        summaryText.text = sb.ToString().TrimEnd();
    }

    private static string FormatDate(string isoDate)
    {
        if (System.DateTime.TryParse(isoDate, out var dt))
            return dt.ToString("MMM d, yyyy");
        return isoDate;
    }

    private static string FormatDateShort(string isoDate)
    {
        if (System.DateTime.TryParse(isoDate, out var dt))
            return dt.ToString("ddd M/d");
        return isoDate;
    }

    void Update()
    {
        if (!waitingForInput) return;
        if (Input.anyKeyDown)
        {
            waitingForInput = false;
            StartCoroutine(FadeOutSequence());
        }
    }

    private IEnumerator FadeInSequence()
    {
        canvasGroup.alpha = 0f;
        float t = 0f;
        while (t < 0.8f)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(t / 0.8f);
            yield return null;
        }
        canvasGroup.alpha = 1f;
        continueText.gameObject.SetActive(true);
        waitingForInput = true;
    }

    private IEnumerator FadeOutSequence()
    {
        continueText.gameObject.SetActive(false);
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(t / 0.6f);
            yield return null;
        }
        overlayCanvas.SetActive(false);
        canvasGroup.alpha = 0f;
        onDismissed?.Invoke();
        onDismissed = null;
    }

    private void BuildUI()
    {
        overlayCanvas = new GameObject("WeekReviewCanvas");
        overlayCanvas.transform.SetParent(transform);
        var canvas = overlayCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;

        var scaler = overlayCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        overlayCanvas.AddComponent<GraphicRaycaster>();
        canvasGroup = overlayCanvas.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        // Background
        var bg = new GameObject("BG");
        bg.transform.SetParent(overlayCanvas.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 1f);

        // Header
        var headerGO = new GameObject("Header");
        headerGO.transform.SetParent(overlayCanvas.transform, false);
        var headerRect = headerGO.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0.1f, 0.82f);
        headerRect.anchorMax = new Vector2(0.9f, 0.95f);
        headerRect.sizeDelta = Vector2.zero;
        headerText = headerGO.AddComponent<TextMeshProUGUI>();
        headerText.fontSize = 26;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.color = new Color(0.85f, 0.85f, 0.9f);

        // Summary body (centered column)
        var bodyGO = new GameObject("Summary");
        bodyGO.transform.SetParent(overlayCanvas.transform, false);
        var bodyRect = bodyGO.AddComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0.2f, 0.1f);
        bodyRect.anchorMax = new Vector2(0.8f, 0.8f);
        bodyRect.sizeDelta = Vector2.zero;
        summaryText = bodyGO.AddComponent<TextMeshProUGUI>();
        summaryText.fontSize = 17;
        summaryText.alignment = TextAlignmentOptions.Top;
        summaryText.color = new Color(0.72f, 0.72f, 0.76f);
        summaryText.enableWordWrapping = true;
        summaryText.richText = true;

        // Continue prompt
        var contGO = new GameObject("Continue");
        contGO.transform.SetParent(overlayCanvas.transform, false);
        var contRect = contGO.AddComponent<RectTransform>();
        contRect.anchorMin = new Vector2(0.3f, 0.03f);
        contRect.anchorMax = new Vector2(0.7f, 0.08f);
        contRect.sizeDelta = Vector2.zero;
        continueText = contGO.AddComponent<TextMeshProUGUI>();
        continueText.text = "Press any key to continue...";
        continueText.fontSize = 16;
        continueText.fontStyle = FontStyles.Italic;
        continueText.alignment = TextAlignmentOptions.Center;
        continueText.color = new Color(0.4f, 0.4f, 0.45f);
        contGO.SetActive(false);

        overlayCanvas.SetActive(false);
    }
}
