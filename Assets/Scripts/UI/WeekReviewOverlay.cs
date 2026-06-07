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

    private void PopulateContent(WeekReviewResponse data)
    {
        headerText.text = $"<color=#D4A878>WEEK IN REVIEW</color>\n{data.start_date}  →  {data.end_date}  ({data.days_advanced} trading days)";

        var sb = new System.Text.StringBuilder();

        if (data.daily_summaries != null)
        {
            double startNw = data.daily_summaries.Length > 0 ? data.daily_summaries[0].net_worth : 0;
            double endNw = data.daily_summaries.Length > 0 ? data.daily_summaries[^1].net_worth : 0;
            double weekChange = endNw - startNw;
            double weekPct = startNw > 0 ? weekChange / startNw * 100 : 0;
            string sign = weekChange >= 0 ? "+" : "";
            string clr = weekChange >= 0 ? "#26BF59" : "#D93838";

            sb.AppendLine($"<b>Net Worth: ${endNw:N2}</b>  <color={clr}>{sign}${weekChange:N2} ({sign}{weekPct:F1}%)</color>");
            sb.AppendLine();

            sb.AppendLine("<color=#666D78>─────────────────────────────</color>");
            sb.AppendLine("<b>DAILY BREAKDOWN</b>");
            sb.AppendLine();

            foreach (var day in data.daily_summaries)
            {
                sb.Append($"  {day.date}  ${day.net_worth:N2}");
                if (day.top_movers != null && day.top_movers.Length > 0)
                {
                    sb.Append("  ");
                    foreach (var m in day.top_movers)
                    {
                        string mSign = m.change_pct >= 0 ? "+" : "";
                        string mClr = m.change_pct >= 0 ? "#26BF59" : "#D93838";
                        sb.Append($"<color={mClr}>{m.ticker} {mSign}{m.change_pct:F1}%</color>  ");
                    }
                }
                sb.AppendLine();
            }
        }

        if (data.unlocked_nodes != null && data.unlocked_nodes.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<color=#666D78>─────────────────────────────</color>");
            sb.AppendLine($"<b><color=#D4A878>NEW CONCEPTS UNLOCKED ({data.unlocked_nodes.Length})</color></b>");
            foreach (var node in data.unlocked_nodes)
                sb.AppendLine($"  • {node.title}");
        }

        summaryText.text = sb.ToString().TrimEnd();
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
        bg.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.06f, 0.96f);

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

        // Summary body (scrollable)
        var bodyGO = new GameObject("Summary");
        bodyGO.transform.SetParent(overlayCanvas.transform, false);
        var bodyRect = bodyGO.AddComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0.12f, 0.12f);
        bodyRect.anchorMax = new Vector2(0.88f, 0.8f);
        bodyRect.sizeDelta = Vector2.zero;
        summaryText = bodyGO.AddComponent<TextMeshProUGUI>();
        summaryText.fontSize = 17;
        summaryText.alignment = TextAlignmentOptions.TopLeft;
        summaryText.color = new Color(0.75f, 0.75f, 0.8f);
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
        continueText.color = new Color(0.5f, 0.5f, 0.55f);
        contGO.SetActive(false);

        overlayCanvas.SetActive(false);
    }
}
