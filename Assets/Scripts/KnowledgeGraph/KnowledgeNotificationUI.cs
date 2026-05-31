using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.API.DTO;

public class KnowledgeNotificationUI : MonoBehaviour
{
    public static KnowledgeNotificationUI Inst { get; private set; }

    private GameObject notificationPanel;
    private TMP_Text titleText;
    private TMP_Text descriptionText;
    private Button learnMoreButton;
    private Button dismissButton;
    private float autoDismissTime = 12f;
    private Image timerBar;

    private UnlockedNodeDTO currentNode;
    private UnlockedNodeDTO queuedNode;
    private float showTimer;
    private bool suppressGeneric;
    private string queuedFeatureLabel;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    void OnEnable()
    {
        if (KnowledgeGraphManager.Inst != null)
        {
            KnowledgeGraphManager.Inst.OnHighPriorityUnlock += ShowNotification;
            KnowledgeGraphManager.Inst.OnPendingCountChanged += OnPendingCountChanged;

            if (KnowledgeGraphManager.Inst.HasPendingPopups())
                DisplayNotification(KnowledgeGraphManager.Inst.DequeuePopup());
            else if (KnowledgeGraphManager.Inst.PendingUnlockedCount > 0)
                ShowGenericNotification(KnowledgeGraphManager.Inst.PendingUnlockedCount);
        }
        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.OnStateChanged += OnPlayerStateChanged;

        ProgressionGates.OnFeatureUnlocked += OnFeatureUnlocked;
    }

    void OnDisable()
    {
        if (KnowledgeGraphManager.Inst != null)
        {
            KnowledgeGraphManager.Inst.OnHighPriorityUnlock -= ShowNotification;
            KnowledgeGraphManager.Inst.OnPendingCountChanged -= OnPendingCountChanged;
        }
        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.OnStateChanged -= OnPlayerStateChanged;

        ProgressionGates.OnFeatureUnlocked -= OnFeatureUnlocked;
    }

    void Update()
    {
        if (notificationPanel == null || !notificationPanel.activeSelf) return;

        showTimer -= Time.deltaTime;
        if (timerBar != null)
            timerBar.fillAmount = Mathf.Clamp01(showTimer / autoDismissTime);
        if (showTimer <= 0f)
            Dismiss();
    }

    private void OnPlayerStateChanged(PlayerState oldState, PlayerState newState)
    {
        if (newState != PlayerState.TRADING && queuedNode != null)
        {
            var node = queuedNode;
            queuedNode = null;
            DisplayNotification(node);
        }
    }

    private void OnPendingCountChanged(int count)
    {
        if (count > 0 && !suppressGeneric && (notificationPanel == null || !notificationPanel.activeSelf))
            ShowGenericNotification(count);

        suppressGeneric = false;
    }

    private void ShowNotification(UnlockedNodeDTO node)
    {
        suppressGeneric = true;

        if (PlayerStateController.Inst != null &&
            PlayerStateController.Inst.State == PlayerState.TRADING)
        {
            queuedNode = node;
            return;
        }

        DisplayNotification(node);
    }

    private void DisplayNotification(UnlockedNodeDTO node)
    {
        currentNode = node;
        showTimer = autoDismissTime;

        if (titleText != null)
        {
            titleText.text = $"New Insight: {node.title}";
            titleText.color = new Color(0.9f, 0.75f, 0.2f, 1f);
        }
        if (descriptionText != null)
        {
            string explanation = GetTriggerExplanation(node.node_id);
            descriptionText.text = !string.IsNullOrEmpty(explanation)
                ? explanation
                : "Press 'Learn More' to explore this concept.";
        }

        notificationPanel.SetActive(true);
    }

    private void ShowGenericNotification(int count)
    {
        currentNode = null;
        showTimer = autoDismissTime;

        if (titleText != null)
        {
            titleText.text = count == 1 ? "1 Insight Available" : $"{count} Insights Available";
            titleText.color = new Color(0.9f, 0.75f, 0.2f, 1f);
        }
        if (descriptionText != null)
            descriptionText.text = "Open the Knowledge Graph to review.";

        notificationPanel.SetActive(true);
    }

    private string GetTriggerExplanation(string nodeId)
    {
        if (KnowledgeGraphManager.Inst?.Nodes == null) return null;
        foreach (var n in KnowledgeGraphManager.Inst.Nodes)
        {
            if (n.id == nodeId)
                return n.trigger_explanation;
        }
        return null;
    }

    private void OnLearnMore()
    {
        Dismiss();
        if (currentNode != null && KnowledgeGraphUI.Inst != null)
            KnowledgeGraphUI.Inst.OpenToNode(currentNode.node_id);
        else if (KnowledgeGraphUI.Inst != null)
            KnowledgeGraphUI.Inst.Open();
    }

    private void Dismiss()
    {
        if (notificationPanel != null)
            notificationPanel.SetActive(false);
        currentNode = null;

        if (queuedFeatureLabel != null)
        {
            var label = queuedFeatureLabel;
            queuedFeatureLabel = null;
            ShowFeatureUnlockDisplay(label);
        }
    }

    private void OnFeatureUnlocked(string nodeId, string featureLabel)
    {
        if (notificationPanel != null && notificationPanel.activeSelf)
        {
            queuedFeatureLabel = featureLabel;
            return;
        }
        ShowFeatureUnlockDisplay(featureLabel);
    }

    private void ShowFeatureUnlockDisplay(string featureLabel)
    {
        currentNode = null;
        showTimer = autoDismissTime;

        if (titleText != null)
        {
            titleText.text = "Feature Unlocked!";
            titleText.color = new Color(0.83f, 0.63f, 1f, 1f);
        }
        if (descriptionText != null)
            descriptionText.text = featureLabel;

        notificationPanel.SetActive(true);
    }

    private void BuildUI()
    {
        var canvasGO = new GameObject("KGNotificationCanvas");
        canvasGO.transform.SetParent(transform);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Panel anchored top-right
        notificationPanel = new GameObject("NotificationPanel");
        notificationPanel.transform.SetParent(canvasGO.transform, false);
        var panelRect = notificationPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-20f, -20f);
        panelRect.sizeDelta = new Vector2(380f, 0f);

        var panelImg = notificationPanel.AddComponent<Image>();
        panelImg.color = new Color(0.1f, 0.1f, 0.14f, 0.95f);

        var vlg = notificationPanel.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.spacing = 6;
        vlg.padding = new RectOffset(16, 16, 14, 14);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        var csf = notificationPanel.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Title
        titleText = MakeText("Title", notificationPanel.transform, 17, FontStyles.Bold,
            new Color(0.9f, 0.75f, 0.2f, 1f));

        // Description
        descriptionText = MakeText("Description", notificationPanel.transform, 14, FontStyles.Normal,
            new Color(0.75f, 0.75f, 0.8f, 1f));

        // Button row
        var buttonRow = new GameObject("Buttons");
        buttonRow.transform.SetParent(notificationPanel.transform, false);
        buttonRow.AddComponent<RectTransform>();
        var hlg = buttonRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childAlignment = TextAnchor.MiddleRight;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(buttonRow.transform, false);
        spacer.AddComponent<RectTransform>();
        var sle = spacer.AddComponent<LayoutElement>();
        sle.flexibleWidth = 1;

        learnMoreButton = MakeButton("LearnMore", buttonRow.transform, "Learn More",
            new Color(0.2f, 0.55f, 0.85f, 1f));
        learnMoreButton.onClick.AddListener(OnLearnMore);

        dismissButton = MakeButton("Dismiss", buttonRow.transform, "Dismiss",
            new Color(0.35f, 0.35f, 0.4f, 1f));
        dismissButton.onClick.AddListener(Dismiss);

        // Countdown timer bar at bottom of panel
        var barGO = new GameObject("TimerBar");
        barGO.transform.SetParent(notificationPanel.transform, false);
        var barRT = barGO.AddComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0f, 0f);
        barRT.anchorMax = new Vector2(1f, 0f);
        barRT.pivot = new Vector2(0f, 0f);
        barRT.anchoredPosition = Vector2.zero;
        barRT.sizeDelta = new Vector2(0f, 3f);
        timerBar = barGO.AddComponent<Image>();
        timerBar.color = new Color(0.9f, 0.75f, 0.2f, 0.5f);
        timerBar.type = Image.Type.Filled;
        timerBar.fillMethod = Image.FillMethod.Horizontal;
        timerBar.fillAmount = 1f;
        timerBar.raycastTarget = false;
        // Exclude from layout so it overlays at the panel bottom edge
        var barLE = barGO.AddComponent<LayoutElement>();
        barLE.ignoreLayout = true;

        notificationPanel.SetActive(false);
    }

    private static TMP_Text MakeText(string name, Transform parent, float size, FontStyles style, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button MakeButton(string name, Transform parent, string label, Color bgColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 30;
        le.preferredWidth = 100;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        textRT.anchoredPosition = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 13;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        return btn;
    }
}
