using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NewspaperHUDButton : MonoBehaviour
{
    public static NewspaperHUDButton Inst { get; private set; }

    [SerializeField] private float buttonSize = 40f;
    [SerializeField] private float rightMargin = 15f;
    [SerializeField] private float topMargin = 15f;

    private GameObject buttonCanvas;
    private Button button;
    private GameObject badgeDot;
    private bool hasUnread;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    void Update()
    {
        bool visible = PlayerStateController.Inst != null
            && PlayerStateController.Inst.State == PlayerState.MOVING;
        if (buttonCanvas != null && buttonCanvas.activeSelf != visible)
            buttonCanvas.SetActive(visible);
    }

    public void MarkUnread()
    {
        hasUnread = true;
        if (badgeDot != null) badgeDot.SetActive(true);
    }

    private void OnClick()
    {
        if (NewspaperUI.Inst == null) return;
        hasUnread = false;
        if (badgeDot != null) badgeDot.SetActive(false);
        NewspaperUI.Inst.Open();
    }

    private void BuildUI()
    {
        buttonCanvas = new GameObject("NewspaperHUDCanvas");
        buttonCanvas.transform.SetParent(transform);
        var canvas = buttonCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;

        var scaler = buttonCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        buttonCanvas.AddComponent<GraphicRaycaster>();

        var btnGO = new GameObject("NewspaperButton");
        btnGO.transform.SetParent(buttonCanvas.transform, false);
        var btnRect = btnGO.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1f, 1f);
        btnRect.anchorMax = new Vector2(1f, 1f);
        btnRect.pivot = new Vector2(1f, 1f);
        btnRect.anchoredPosition = new Vector2(-rightMargin, -topMargin);
        btnRect.sizeDelta = new Vector2(buttonSize, buttonSize);

        var btnImage = btnGO.AddComponent<Image>();
        btnImage.color = new Color(0.15f, 0.16f, 0.2f, 0.85f);

        button = btnGO.AddComponent<Button>();
        button.targetGraphic = btnImage;
        var colors = button.colors;
        colors.highlightedColor = new Color(0.25f, 0.26f, 0.32f, 0.95f);
        colors.pressedColor = new Color(0.1f, 0.1f, 0.14f, 1f);
        button.colors = colors;
        button.onClick.AddListener(OnClick);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
        labelRect.anchoredPosition = Vector2.zero;
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text = "▤";
        label.fontSize = 22;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.85f, 0.8f, 0.7f, 1f);
        label.raycastTarget = false;

        badgeDot = new GameObject("UnreadBadge");
        badgeDot.transform.SetParent(btnGO.transform, false);
        var dotRect = badgeDot.AddComponent<RectTransform>();
        dotRect.anchorMin = new Vector2(1f, 1f);
        dotRect.anchorMax = new Vector2(1f, 1f);
        dotRect.pivot = new Vector2(0.5f, 0.5f);
        dotRect.anchoredPosition = new Vector2(-4f, -4f);
        dotRect.sizeDelta = new Vector2(10f, 10f);
        var dotImage = badgeDot.AddComponent<Image>();
        dotImage.color = new Color(0.9f, 0.3f, 0.2f, 1f);
        badgeDot.SetActive(false);
    }
}
