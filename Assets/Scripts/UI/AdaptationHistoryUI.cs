using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AdaptationHistoryUI : MonoBehaviour
{
    public static AdaptationHistoryUI Inst { get; private set; }

    private GameObject overlayCanvas;
    private GameObject panel;
    private TMP_Text headerText;
    private Transform contentParent;
    private ScrollRect scrollRect;

    private bool isOpen;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Tab)) return;

        if (isOpen)
        {
            Close();
            return;
        }

        var psc = PlayerStateController.Inst;
        if (psc != null && psc.State != PlayerState.MOVING) return;

        Open();
    }

    private void Open()
    {
        if (isOpen) return;
        isOpen = true;

        PopulateContent();
        panel.SetActive(true);
        scrollRect.verticalNormalizedPosition = 1f;

        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.OpenUI(PlayerState.TRADING, ClosePanel);
    }

    public void Close()
    {
        if (!isOpen) return;

        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.MOVING);
        else
            ClosePanel();
    }

    private void ClosePanel()
    {
        isOpen = false;
        if (panel != null)
            panel.SetActive(false);
    }

    private void PopulateContent()
    {
        foreach (Transform child in contentParent)
        {
            if (child.name != "Header")
                Destroy(child.gameObject);
        }

        var nodes = KnowledgeGraphManager.Inst?.Nodes;
        if (nodes == null || nodes.Length == 0)
        {
            AddText("No adaptations yet.", 16, FontStyles.Italic, new Color(0.5f, 0.5f, 0.55f));
            return;
        }

        // Active insights (unlocked, not yet completed)
        bool hasActive = false;
        foreach (var n in nodes)
        {
            if (n.status != "unlocked") continue;
            if (!hasActive)
            {
                AddSpacer(6);
                AddSectionHeader("ACTIVE INSIGHTS");
                hasActive = true;
            }
            AddNodeEntry(n);
        }

        // Completed
        bool hasCompleted = false;
        foreach (var n in nodes)
        {
            if (n.status != "completed") continue;
            if (!hasCompleted)
            {
                AddSpacer(hasActive ? 16 : 6);
                AddSectionHeader("COMPLETED");
                hasCompleted = true;
            }
            AddNodeEntry(n);
        }

        if (!hasActive && !hasCompleted)
            AddText("No adaptations yet.", 16, FontStyles.Italic, new Color(0.5f, 0.5f, 0.55f));
    }

    private void AddNodeEntry(Game.API.DTO.KnowledgeNodeStateDTO node)
    {
        string catLabel = FormatCategory(node.category);
        string catColor = GetCategoryColor(node.category);

        string dateStr = "";
        if (!string.IsNullOrEmpty(node.completed_at))
            dateStr = $"Completed: {FormatDate(node.completed_at)}";
        else if (!string.IsNullOrEmpty(node.unlocked_at))
            dateStr = $"Triggered: {FormatDate(node.unlocked_at)}";

        var sb = new System.Text.StringBuilder();
        sb.Append($"<b>{node.title}</b>  <color={catColor}><size=13>{catLabel}</size></color>");
        if (!string.IsNullOrEmpty(dateStr))
            sb.Append($"\n<color=#888888><size=14>{dateStr}</size></color>");
        if (!string.IsNullOrEmpty(node.trigger_explanation))
            sb.Append($"\n<color=#AAA5A0><size=14>\"{node.trigger_explanation}\"</size></color>");

        AddText(sb.ToString(), 16, FontStyles.Normal, new Color(0.85f, 0.85f, 0.9f));
        AddSpacer(8);
    }

    private void AddSectionHeader(string text)
    {
        AddDivider();
        AddSpacer(4);

        string icon = text.Contains("ACTIVE") ? "●" : "✓";
        AddText($"{icon}  {text}", 14, FontStyles.Bold, new Color(0.85f, 0.72f, 0.4f));
        AddSpacer(8);
    }

    private TMP_Text AddText(string text, float size, FontStyles style, Color color)
    {
        var go = new GameObject("Entry");
        go.transform.SetParent(contentParent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.enableWordWrapping = true;
        tmp.richText = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    private void AddDivider()
    {
        var go = new GameObject("Divider");
        go.transform.SetParent(contentParent, false);
        go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 1;
        le.flexibleWidth = 1;
    }

    private void AddSpacer(float height)
    {
        var go = new GameObject("Spacer");
        go.transform.SetParent(contentParent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
    }

    private static string FormatDate(string isoDate)
    {
        if (string.IsNullOrEmpty(isoDate)) return "";
        if (System.DateTime.TryParse(isoDate, out var dt))
            return dt.ToString("MMMM d, yyyy");
        return isoDate;
    }

    private static string FormatCategory(string category)
    {
        if (string.IsNullOrEmpty(category)) return "";
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo
            .ToTitleCase(category.Replace("_", " "));
    }

    private static string GetCategoryColor(string category)
    {
        return category switch
        {
            "fundamentals" => "#5B9BD5",
            "order_types" => "#70AD47",
            "risk_management" => "#ED7D31",
            "trading_psychology" => "#BF60B0",
            "game_mechanics" => "#A0A0A0",
            "strategies" => "#FFC000",
            "market_events" => "#FF6B6B",
            _ => "#AAAAAA"
        };
    }

    private void BuildUI()
    {
        overlayCanvas = new GameObject("AdaptationHistoryCanvas");
        overlayCanvas.transform.SetParent(transform);
        var canvas = overlayCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 88;

        var scaler = overlayCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        overlayCanvas.AddComponent<GraphicRaycaster>();

        panel = new GameObject("Panel");
        panel.transform.SetParent(overlayCanvas.transform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        Stretch(panelRect);

        // Dark background — click to close
        var bg = new GameObject("Background");
        bg.transform.SetParent(panel.transform, false);
        Stretch(bg.AddComponent<RectTransform>());
        bg.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.06f, 1f);
        var bgBtn = bg.AddComponent<Button>();
        bgBtn.transition = Selectable.Transition.None;
        bgBtn.onClick.AddListener(Close);

        // Scroll area
        var scrollGO = new GameObject("Scroll");
        scrollGO.transform.SetParent(panel.transform, false);
        var scrollRectTransform = scrollGO.AddComponent<RectTransform>();
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = new Vector2(80f, 50f);
        scrollRectTransform.offsetMax = new Vector2(-80f, -50f);

        scrollGO.AddComponent<Image>().color = Color.clear;
        scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.scrollSensitivity = 30f;

        // Viewport
        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGO.transform, false);
        var viewportRect = viewport.AddComponent<RectTransform>();
        Stretch(viewportRect);
        viewport.AddComponent<RectMask2D>();
        viewport.AddComponent<Image>().color = Color.clear;
        scrollRect.viewport = viewportRect;

        // Content
        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 0f);
        contentParent = content.transform;

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 4;
        vlg.padding = new RectOffset(20, 20, 10, 20);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRect;

        // Header (permanent child)
        var headerGO = new GameObject("Header");
        headerGO.transform.SetParent(contentParent, false);
        headerGO.AddComponent<RectTransform>();
        headerText = headerGO.AddComponent<TextMeshProUGUI>();
        headerText.text = "<color=#D4A878>ADAPTATION HISTORY</color>\n<size=15><color=#666D78>How the game responded to your decisions</color></size>";
        headerText.fontSize = 26;
        headerText.fontStyle = FontStyles.Bold;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.color = new Color(0.85f, 0.85f, 0.9f);
        headerText.enableWordWrapping = true;
        headerText.richText = true;
        headerText.raycastTarget = false;

        panel.SetActive(false);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }
}
