using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

public static class KnowledgeGraphUIBuilder
{
    private static readonly Color DarkBg = new Color(0.1f, 0.1f, 0.15f, 0.95f);
    private static readonly Color PanelBg = new Color(0.15f, 0.15f, 0.2f, 1f);
    private static readonly Color BtnColor = new Color(0.2f, 0.6f, 0.9f, 1f);
    private static readonly Color BtnDanger = new Color(0.8f, 0.2f, 0.2f, 1f);
    private static readonly Color BtnGreen = new Color(0.2f, 0.7f, 0.3f, 1f);
    private static readonly Color BadgeRed = new Color(0.9f, 0.2f, 0.2f, 1f);

    // ─── 1) Full-screen Knowledge Graph Canvas ───────────────────────

    [MenuItem("Tools/Knowledge Graph/Create Graph UI Canvas")]
    public static void CreateGraphUICanvas()
    {
        var canvasGO = CreateCanvas("KnowledgeGraphCanvas", 10);
        var canvasRT = canvasGO.GetComponent<RectTransform>();

        // ── Graph Panel (full screen dark overlay) ──
        var graphPanel = CreatePanel("GraphPanel", canvasRT, DarkBg);
        Stretch(graphPanel);
        graphPanel.gameObject.SetActive(false);

        // Close button (top-right X)
        var closeBtn = CreateButton("CloseButton", graphPanel, "X", BtnDanger, 50, 50);
        AnchorTopRight(closeBtn.GetComponent<RectTransform>(), new Vector2(-30, -30));

        // Title label
        var titleLabel = CreateTMP("GraphTitle", graphPanel, "Knowledge Graph",
            fontSize: 28, alignment: TextAlignmentOptions.TopLeft);
        var titleRT = titleLabel.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 1);
        titleRT.anchorMax = new Vector2(0.5f, 1);
        titleRT.pivot = new Vector2(0, 1);
        titleRT.anchoredPosition = new Vector2(30, -20);
        titleRT.sizeDelta = new Vector2(400, 50);

        // Graph Container (where nodes and edges spawn)
        var graphContainer = new GameObject("GraphContainer", typeof(RectTransform));
        graphContainer.transform.SetParent(graphPanel, false);
        var gcRT = graphContainer.GetComponent<RectTransform>();
        Stretch(gcRT);
        gcRT.offsetMin = new Vector2(20, 20);
        gcRT.offsetMax = new Vector2(-20, -80);
        gcRT.pivot = new Vector2(0.5f, 0.5f);

        // ── Detail Panel (centered article popup) ──
        var detailPanel = CreatePanel("DetailPanel", graphPanel, PanelBg);
        var dpRT = detailPanel.GetComponent<RectTransform>();
        dpRT.anchorMin = new Vector2(0.5f, 0.5f);
        dpRT.anchorMax = new Vector2(0.5f, 0.5f);
        dpRT.pivot = new Vector2(0.5f, 0.5f);
        dpRT.anchoredPosition = Vector2.zero;
        dpRT.sizeDelta = new Vector2(520, 400);
        detailPanel.gameObject.SetActive(false);

        var detailTitle = CreateTMP("DetailTitle", dpRT, "Node Title",
            fontSize: 24, alignment: TextAlignmentOptions.Center);
        var dtRT = detailTitle.GetComponent<RectTransform>();
        dtRT.anchorMin = new Vector2(0, 1);
        dtRT.anchorMax = new Vector2(1, 1);
        dtRT.pivot = new Vector2(0.5f, 1);
        dtRT.anchoredPosition = new Vector2(0, -20);
        dtRT.sizeDelta = new Vector2(-40, 40);

        var detailStatus = CreateTMP("DetailStatus", dpRT, "",
            fontSize: 13, alignment: TextAlignmentOptions.Center);
        detailStatus.fontStyle = FontStyles.Italic;
        var dsRT = detailStatus.GetComponent<RectTransform>();
        dsRT.anchorMin = new Vector2(0, 1);
        dsRT.anchorMax = new Vector2(1, 1);
        dsRT.pivot = new Vector2(0.5f, 1);
        dsRT.anchoredPosition = new Vector2(0, -60);
        dsRT.sizeDelta = new Vector2(-40, 22);

        var detailContent = CreateTMP("DetailContent", dpRT, "Node description goes here.",
            fontSize: 16, alignment: TextAlignmentOptions.TopLeft);
        detailContent.enableWordWrapping = true;
        detailContent.overflowMode = TextOverflowModes.Ellipsis;
        var dcRT = detailContent.GetComponent<RectTransform>();
        dcRT.anchorMin = new Vector2(0, 0.15f);
        dcRT.anchorMax = new Vector2(1, 1);
        dcRT.pivot = new Vector2(0.5f, 1);
        dcRT.anchoredPosition = new Vector2(0, -90);
        dcRT.sizeDelta = new Vector2(-40, 0);
        dcRT.offsetMin = new Vector2(20, dcRT.offsetMin.y);
        dcRT.offsetMax = new Vector2(-20, dcRT.offsetMax.y);

        var completeBtn = CreateButton("DismissButton", dpRT, "Close", BtnColor, 140, 36);
        var cbRT = completeBtn.GetComponent<RectTransform>();
        cbRT.anchorMin = new Vector2(0.5f, 0);
        cbRT.anchorMax = new Vector2(0.5f, 0);
        cbRT.pivot = new Vector2(0.5f, 0);
        cbRT.anchoredPosition = new Vector2(0, 16);

        // ── Attach KnowledgeGraphUI component and wire fields ──
        var ui = canvasGO.AddComponent<KnowledgeGraphUI>();
        var so = new SerializedObject(ui);

        // Load the existing node prefab
        var nodePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Knowledge Graph/Knowledge Node.prefab");

        so.FindProperty("graphPanel").objectReferenceValue = graphPanel.gameObject;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        so.FindProperty("graphContainer").objectReferenceValue = gcRT;
        so.FindProperty("nodePrefab").objectReferenceValue = nodePrefab;
        so.FindProperty("detailPanel").objectReferenceValue = detailPanel.gameObject;
        so.FindProperty("detailTitle").objectReferenceValue = detailTitle;
        so.FindProperty("detailContent").objectReferenceValue = detailContent;
        so.FindProperty("detailStatus").objectReferenceValue = detailStatus;
        so.FindProperty("completeButton").objectReferenceValue = completeBtn;
        so.ApplyModifiedProperties();

        if (nodePrefab == null)
            Debug.LogWarning("[KGBuilder] Node prefab not found at Assets/Prefabs/Knowledge Graph/Knowledge Node.prefab — assign it manually.");

        Selection.activeGameObject = canvasGO;
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Knowledge Graph Canvas");
        Debug.Log("[KGBuilder] Created KnowledgeGraphCanvas. Select it to verify wiring in the Inspector.");
    }

    // ─── 2) Wire PlayerObjectivesUI onto existing Player UI Canvas prefab ──

    [MenuItem("Tools/Knowledge Graph/Wire Objectives UI (Player UI Canvas)")]
    public static void WireObjectivesUI()
    {
        var prefabPath = "Assets/Prefabs/Player UI Canvas.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[KGBuilder] Could not find prefab at {prefabPath}");
            return;
        }

        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null) return;

        var objectivesList = instance.transform.Find("Player Objectives/Text (TMP)/Objectives List");
        if (objectivesList == null)
        {
            Debug.LogError("[KGBuilder] Could not find 'Player Objectives/Text (TMP)/Objectives List' in prefab hierarchy.");
            Object.DestroyImmediate(instance);
            return;
        }

        var ui = instance.GetComponent<PlayerObjectivesUI>();
        if (ui == null)
            ui = instance.AddComponent<PlayerObjectivesUI>();

        var so = new SerializedObject(ui);
        so.FindProperty("objectivesList").objectReferenceValue = objectivesList;
        so.ApplyModifiedProperties();

        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);

        Debug.Log("[KGBuilder] Wired PlayerObjectivesUI onto Player UI Canvas prefab. The 'objectivesList' field points to the VerticalLayoutGroup.");
    }

    // ─── 3) Node Prefab (if the existing one is empty/broken) ────────

    [MenuItem("Tools/Knowledge Graph/Rebuild Node Prefab")]
    public static void RebuildNodePrefab()
    {
        var go = new GameObject("Knowledge Node", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(120, 50);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        var label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(go.transform, false);
        var tmp = label.AddComponent<TextMeshProUGUI>();
        tmp.text = "Node";
        tmp.fontSize = 12;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        var labelRT = label.GetComponent<RectTransform>();
        Stretch(labelRT);
        labelRT.offsetMin = new Vector2(5, 5);
        labelRT.offsetMax = new Vector2(-5, -5);

        var path = "Assets/Prefabs/Knowledge Graph/Knowledge Node.prefab";
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Knowledge Graph"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Knowledge Graph");

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log($"[KGBuilder] Saved node prefab to {path}");
    }

    // ─── 4) In-Room Interaction Object ───────────────────────────────

    [MenuItem("Tools/Knowledge Graph/Create Interaction Object (Room)")]
    public static void CreateInteractionObject()
    {
        var go = new GameObject("KnowledgeGraphTerminal");
        go.transform.position = Vector3.zero;

        var interaction = go.AddComponent<KnowledgeGraphInteraction>();

        // Badge indicator: a small world-space canvas above the object
        var badgeCanvas = CreateCanvas("BadgeCanvas", 0, RenderMode.WorldSpace);
        badgeCanvas.transform.SetParent(go.transform, false);
        badgeCanvas.transform.localPosition = new Vector3(0, 2.5f, 0);
        badgeCanvas.transform.localScale = Vector3.one * 0.02f;
        var bcRT = badgeCanvas.GetComponent<RectTransform>();
        bcRT.sizeDelta = new Vector2(100, 100);

        var badge = CreatePanel("BadgeIndicator", bcRT, BadgeRed);
        var badgeRT = badge.GetComponent<RectTransform>();
        badgeRT.anchorMin = badgeRT.anchorMax = badgeRT.pivot = new Vector2(0.5f, 0.5f);
        badgeRT.sizeDelta = new Vector2(60, 60);
        badge.gameObject.SetActive(false);

        var countText = CreateTMP("CountText", badgeRT, "0",
            fontSize: 30, alignment: TextAlignmentOptions.Center);
        var ctRT = countText.GetComponent<RectTransform>();
        Stretch(ctRT);

        var so = new SerializedObject(interaction);
        so.FindProperty("badgeIndicator").objectReferenceValue = badge.gameObject;
        so.FindProperty("badgeCountText").objectReferenceValue = countText;
        so.ApplyModifiedProperties();

        Selection.activeGameObject = go;
        Undo.RegisterCreatedObjectUndo(go, "Create KnowledgeGraph Interaction Object");
        Debug.Log("[KGBuilder] Created KnowledgeGraphTerminal. Position it in your Room scene, then wire its OpenGraph() to an InteractionZone.");
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static GameObject CreateCanvas(string name, int sortOrder, RenderMode mode = RenderMode.ScreenSpaceOverlay)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = mode;
        canvas.sortingOrder = sortOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        return go;
    }

    private static RectTransform CreatePanel(string name, RectTransform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go.GetComponent<RectTransform>();
    }

    private static Button CreateButton(string name, RectTransform parent, string label, Color bgColor, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);

        go.GetComponent<Image>().color = bgColor;

        var txt = new GameObject("Label", typeof(RectTransform));
        txt.transform.SetParent(go.transform, false);
        var tmp = txt.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        Stretch(txt.GetComponent<RectTransform>());

        return go.GetComponent<Button>();
    }

    private static TMP_Text CreateTMP(string name, RectTransform parent, string text,
        float fontSize = 16, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        return tmp;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void AnchorTopRight(RectTransform rt, Vector2 offset)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = offset;
    }
}
