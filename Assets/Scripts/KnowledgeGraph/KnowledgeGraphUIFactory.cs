using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class KnowledgeGraphUIFactory
{
    private static readonly Color DarkBg = new(0.1f, 0.1f, 0.15f, 0.95f);
    private static readonly Color PanelBg = new(0.15f, 0.15f, 0.2f, 1f);
    private static readonly Color BtnBlue = new(0.2f, 0.6f, 0.9f, 1f);
    private static readonly Color BtnRed = new(0.8f, 0.2f, 0.2f, 1f);

    public struct Result
    {
        public GameObject canvasGO;
        public KnowledgeGraphUI ui;
        public GameObject graphPanel;
        public Button closeButton;
        public RectTransform graphContainer;
        public GameObject detailPanel;
        public TMP_Text detailTitle;
        public TMP_Text detailContent;
        public TMP_Text detailStatus;
        public Button completeButton;
        public GameObject nodePrefab;
    }

    public static Result Build(GameObject existingNodePrefab = null)
    {
        var canvasGO = new GameObject("KnowledgeGraphCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Graph Panel
        var graphPanel = CreatePanel("GraphPanel", canvasGO.transform, DarkBg);
        Stretch(graphPanel.GetComponent<RectTransform>());
        graphPanel.SetActive(false);

        // Close button
        var closeBtn = CreateButton("CloseButton", graphPanel.transform, "X", BtnRed, 32, 32);
        var cbRT = closeBtn.GetComponent<RectTransform>();
        cbRT.anchorMin = cbRT.anchorMax = cbRT.pivot = new Vector2(1, 1);
        cbRT.anchoredPosition = new Vector2(-16, -16);

        // Title label
        var titleTMP = CreateTMP("GraphTitle", graphPanel.transform, "Knowledge Graph", 28, TextAlignmentOptions.TopLeft);
        var titleRT = titleTMP.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 1); titleRT.anchorMax = new Vector2(0.5f, 1);
        titleRT.pivot = new Vector2(0, 1); titleRT.anchoredPosition = new Vector2(30, -20);
        titleRT.sizeDelta = new Vector2(400, 50);

        // Graph Container
        var graphContainer = new GameObject("GraphContainer", typeof(RectTransform));
        graphContainer.transform.SetParent(graphPanel.transform, false);
        var gcRT = graphContainer.GetComponent<RectTransform>();
        Stretch(gcRT);
        gcRT.offsetMin = new Vector2(20, 20);
        gcRT.offsetMax = new Vector2(-20, -80);
        gcRT.pivot = new Vector2(0.5f, 0.5f);

        // Detail Panel — right-side inspector (matches Room scene layout)
        var detailPanel = CreatePanel("DetailPanel", graphPanel.transform, PanelBg);
        var dpRT = detailPanel.GetComponent<RectTransform>();
        dpRT.anchorMin = new Vector2(1, 0);
        dpRT.anchorMax = new Vector2(1, 1);
        dpRT.pivot = new Vector2(1, 0.5f);
        dpRT.anchoredPosition = new Vector2(-10, 0);
        dpRT.sizeDelta = new Vector2(290, -40);
        detailPanel.SetActive(false);

        var detailTitle = CreateTMP("DetailTitle", detailPanel.transform, "", 24, TextAlignmentOptions.Center);
        var dtRT = detailTitle.GetComponent<RectTransform>();
        dtRT.anchorMin = new Vector2(0, 1); dtRT.anchorMax = new Vector2(1, 1);
        dtRT.pivot = new Vector2(0.5f, 1); dtRT.anchoredPosition = new Vector2(0, -15);
        dtRT.sizeDelta = new Vector2(-30, 40);

        var detailStatus = CreateTMP("DetailStatus", detailPanel.transform, "", 13, TextAlignmentOptions.Center);
        detailStatus.fontStyle = FontStyles.Italic;
        var dsRT = detailStatus.GetComponent<RectTransform>();
        dsRT.anchorMin = new Vector2(0, 0.2f); dsRT.anchorMax = new Vector2(1, 0.3f);
        dsRT.pivot = new Vector2(0.5f, 0.5f); dsRT.anchoredPosition = Vector2.zero;
        dsRT.sizeDelta = new Vector2(-30, 0);

        var detailContent = CreateTMP("DetailContent", detailPanel.transform, "", 16, TextAlignmentOptions.TopLeft);
        detailContent.enableWordWrapping = true;
        detailContent.overflowMode = TextOverflowModes.Ellipsis;
        var dcRT = detailContent.GetComponent<RectTransform>();
        dcRT.anchorMin = new Vector2(0, 0.3f); dcRT.anchorMax = new Vector2(1, 0.85f);
        dcRT.pivot = new Vector2(0.5f, 0.5f); dcRT.anchoredPosition = Vector2.zero;
        dcRT.sizeDelta = new Vector2(-30, 0);

        var completeBtn = CreateButton("DismissButton", detailPanel.transform, "Complete", BtnBlue, 200, 40);
        var compRT = completeBtn.GetComponent<RectTransform>();
        compRT.anchorMin = compRT.anchorMax = new Vector2(0.5f, 0);
        compRT.pivot = new Vector2(0.5f, 0);
        compRT.anchoredPosition = new Vector2(0, 20);

        // Node prefab
        var nodePrefab = existingNodePrefab;
        if (nodePrefab == null)
            nodePrefab = BuildNodePrefab(canvasGO.transform);

        // Attach component + wire fields
        var ui = canvasGO.AddComponent<KnowledgeGraphUI>();
        var t = typeof(KnowledgeGraphUI);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        t.GetField("graphPanel", flags).SetValue(ui, graphPanel);
        t.GetField("closeButton", flags).SetValue(ui, closeBtn);
        t.GetField("graphContainer", flags).SetValue(ui, gcRT);
        t.GetField("nodePrefab", flags).SetValue(ui, nodePrefab);
        t.GetField("detailPanel", flags).SetValue(ui, detailPanel);
        t.GetField("detailTitle", flags).SetValue(ui, detailTitle);
        t.GetField("detailContent", flags).SetValue(ui, detailContent);
        t.GetField("detailStatus", flags).SetValue(ui, detailStatus);
        t.GetField("completeButton", flags).SetValue(ui, completeBtn);

        return new Result
        {
            canvasGO = canvasGO,
            ui = ui,
            graphPanel = graphPanel,
            closeButton = closeBtn,
            graphContainer = gcRT,
            detailPanel = detailPanel,
            detailTitle = detailTitle,
            detailContent = detailContent,
            detailStatus = detailStatus,
            completeButton = completeBtn,
            nodePrefab = nodePrefab,
        };
    }

    public static GameObject BuildNodePrefab(Transform parent = null)
    {
        var go = new GameObject("KnowledgeNodePrefab", typeof(RectTransform), typeof(Image), typeof(Button));
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 50);
        go.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        var label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(go.transform, false);
        var tmp = label.AddComponent<TextMeshProUGUI>();
        tmp.text = "Node"; tmp.fontSize = 12;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
        tmp.enableWordWrapping = true; tmp.overflowMode = TextOverflowModes.Ellipsis;
        var lrt = label.GetComponent<RectTransform>();
        Stretch(lrt);
        lrt.offsetMin = new Vector2(5, 5);
        lrt.offsetMax = new Vector2(-5, -5);

        go.SetActive(false);
        if (parent != null) go.transform.SetParent(parent, false);

        return go;
    }

    // ── Helpers ──

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static Button CreateButton(string name, Transform parent, string label, Color bgColor, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
        go.GetComponent<Image>().color = bgColor;

        var txt = new GameObject("Label", typeof(RectTransform));
        txt.transform.SetParent(go.transform, false);
        var tmp = txt.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
        Stretch(txt.GetComponent<RectTransform>());

        return go.GetComponent<Button>();
    }

    private static TMP_Text CreateTMP(string name, Transform parent, string text,
        float fontSize = 16, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = fontSize;
        tmp.alignment = alignment; tmp.color = Color.white;
        if (tmp.font == null)
        {
            var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null) tmp.font = font;
        }
        return tmp;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
