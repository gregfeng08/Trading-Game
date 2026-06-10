using System.Threading.Tasks;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.API;
using Game.API.DTO;

public class NewspaperUI : MonoBehaviour
{
    public static NewspaperUI Inst { get; private set; }

    private GameObject overlayCanvas;
    private GameObject newspaperPanel;
    private RawImage newspaperImage;
    private RectTransform contentRect;
    private ScrollRect scrollRect;
    private RectTransform scrollRectTransform;
    private TMP_Text loadingText;

    private Texture2D currentTexture;
    private bool isLoading;
    private string loadedDate;
    private Coroutine loadingAnimCoroutine;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    void OnDestroy()
    {
        if (currentTexture != null)
            Destroy(currentTexture);
    }

    public void Open()
    {
        string date = GamePhaseManager.Inst != null ? GamePhaseManager.Inst.CurrentDate : null;

        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.OpenUI(PlayerState.TRADING, ClosePanel);

        Show(date);
    }

    public async void Show(string date = null)
    {
        if (newspaperPanel == null) return;

        newspaperPanel.SetActive(true);

        if (date == loadedDate && currentTexture != null)
        {
            SetLoading(false);
            ApplyTexture();
            return;
        }

        SetLoading(true);
        newspaperImage.enabled = false;

        await LoadNewspaper(date);
        _ = RecordNewspaperRead(date);
    }

    private async Task RecordNewspaperRead(string date)
    {
        try
        {
            var req = new PlayerEventRequestDTO
            {
                entity_id = APIBootstrapper.EntityDbId,
                event_type = "newspaper_read",
                metadata_json = $"{{\"date\":\"{date}\"}}"
            };
            await PlayerEventsAPI.RecordEvent(req);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[NewspaperUI] Failed to record read event: {ex.Message}");
        }
    }

    public void Hide()
    {
        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.MOVING);
        else
            ClosePanel();
    }

    private void ClosePanel()
    {
        if (newspaperPanel != null)
            newspaperPanel.SetActive(false);
    }

    private async Task LoadNewspaper(string date)
    {
        if (isLoading) return;
        isLoading = true;

        try
        {
            var tex = await NewspaperAPI.GetNewspaperImage(date, APIBootstrapper.EntityDbId);
            if (tex == null)
            {
                Debug.LogWarning("[NewspaperUI] Failed to load newspaper image");
                SetLoading(false, "Failed to load newspaper.");
                return;
            }

            if (currentTexture != null)
                Destroy(currentTexture);

            currentTexture = tex;
            loadedDate = date;
            SetLoading(false);
            ApplyTexture();
        }
        finally
        {
            isLoading = false;
        }
    }

    private void SetLoading(bool loading, string message = null)
    {
        if (loadingAnimCoroutine != null)
        {
            StopCoroutine(loadingAnimCoroutine);
            loadingAnimCoroutine = null;
        }

        loadingText.gameObject.SetActive(loading || message != null);

        if (loading && message == null)
            loadingAnimCoroutine = StartCoroutine(AnimateLoadingDots());
        else
            loadingText.text = message ?? "";

        if (!loading && message == null)
            newspaperImage.enabled = true;
    }

    private IEnumerator AnimateLoadingDots()
    {
        string[] frames = { "Loading newspaper.", "Loading newspaper..", "Loading newspaper..." };
        int i = 0;
        while (true)
        {
            loadingText.text = frames[i % frames.Length];
            i++;
            yield return new WaitForSeconds(0.4f);
        }
    }

    private void ApplyTexture()
    {
        if (currentTexture == null) return;

        newspaperImage.texture = currentTexture;
        newspaperImage.enabled = true;

        FitToViewportWidth();

        scrollRect.verticalNormalizedPosition = 1f;
    }

    private void FitToViewportWidth()
    {
        if (currentTexture == null || scrollRectTransform == null) return;

        float viewportWidth = scrollRectTransform.rect.width;
        float texAspect = (float)currentTexture.height / currentTexture.width;
        float fitHeight = viewportWidth * texAspect;

        contentRect.sizeDelta = new Vector2(viewportWidth, fitHeight);
    }

    private void BuildUI()
    {
        overlayCanvas = new GameObject("NewspaperCanvas");
        overlayCanvas.transform.SetParent(transform);
        var canvas = overlayCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        var scaler = overlayCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        overlayCanvas.AddComponent<GraphicRaycaster>();

        // Panel (toggled on/off)
        newspaperPanel = new GameObject("NewspaperPanel");
        newspaperPanel.transform.SetParent(overlayCanvas.transform, false);
        var panelRect = newspaperPanel.AddComponent<RectTransform>();
        Stretch(panelRect);

        // Dark background — clicking it closes the newspaper
        var bg = new GameObject("Background");
        bg.transform.SetParent(newspaperPanel.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        Stretch(bgRect);
        var bgImage = bg.AddComponent<Image>();
        bgImage.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);
        var bgButton = bg.AddComponent<Button>();
        bgButton.transition = Selectable.Transition.None;
        bgButton.onClick.AddListener(Hide);

        // Scroll area
        var scrollGO = new GameObject("Scroll");
        scrollGO.transform.SetParent(newspaperPanel.transform, false);
        scrollRectTransform = scrollGO.AddComponent<RectTransform>();
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = new Vector2(60f, 40f);
        scrollRectTransform.offsetMax = new Vector2(-80f, -60f);

        var scrollImage = scrollGO.AddComponent<Image>();
        scrollImage.color = Color.clear;
        scrollImage.raycastTarget = true;

        scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.1f;
        scrollRect.scrollSensitivity = 30f;

        // Viewport (masked area)
        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var viewportRect = viewportGO.AddComponent<RectTransform>();
        Stretch(viewportRect);
        viewportGO.AddComponent<RectMask2D>();
        var viewportImage = viewportGO.AddComponent<Image>();
        viewportImage.color = Color.clear;
        viewportImage.raycastTarget = true;

        scrollRect.viewport = viewportRect;

        // Content container — centered, explicit size set by FitToViewportWidth
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 1f);
        contentRect.anchorMax = new Vector2(0.5f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(800f, 1100f);

        scrollRect.content = contentRect;

        // Vertical scrollbar — right side, always visible
        var scrollbarGO = new GameObject("Scrollbar");
        scrollbarGO.transform.SetParent(newspaperPanel.transform, false);
        var scrollbarRect = scrollbarGO.AddComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.anchoredPosition = new Vector2(-20f, 0f);
        scrollbarRect.offsetMin = new Vector2(scrollbarRect.offsetMin.x, 40f);
        scrollbarRect.offsetMax = new Vector2(scrollbarRect.offsetMax.x, -60f);
        scrollbarRect.sizeDelta = new Vector2(10f, scrollbarRect.sizeDelta.y);

        var scrollbarImage = scrollbarGO.AddComponent<Image>();
        scrollbarImage.color = new Color(0.18f, 0.18f, 0.22f, 0.6f);

        var scrollbar = scrollbarGO.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        var handleArea = new GameObject("HandleArea");
        handleArea.transform.SetParent(scrollbarGO.transform, false);
        var handleAreaRect = handleArea.AddComponent<RectTransform>();
        Stretch(handleAreaRect);

        var handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(handleArea.transform, false);
        var handleRect = handleGO.AddComponent<RectTransform>();
        Stretch(handleRect);
        var handleImage = handleGO.AddComponent<Image>();
        handleImage.color = new Color(0.55f, 0.55f, 0.6f, 0.8f);

        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        var sbColors = scrollbar.colors;
        sbColors.highlightedColor = new Color(0.7f, 0.7f, 0.75f, 0.9f);
        sbColors.pressedColor = new Color(0.85f, 0.85f, 0.9f, 1f);
        scrollbar.colors = sbColors;

        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        // Newspaper image
        var imgGO = new GameObject("NewspaperImage");
        imgGO.transform.SetParent(contentGO.transform, false);
        var imgRect = imgGO.AddComponent<RectTransform>();
        Stretch(imgRect);
        newspaperImage = imgGO.AddComponent<RawImage>();
        newspaperImage.enabled = false;
        newspaperImage.raycastTarget = false;

        // Loading text
        var loadGO = new GameObject("LoadingText");
        loadGO.transform.SetParent(newspaperPanel.transform, false);
        var loadRect = loadGO.AddComponent<RectTransform>();
        loadRect.anchorMin = new Vector2(0.5f, 0.5f);
        loadRect.anchorMax = new Vector2(0.5f, 0.5f);
        loadRect.sizeDelta = new Vector2(500, 60);
        loadRect.anchoredPosition = Vector2.zero;
        loadingText = loadGO.AddComponent<TextMeshProUGUI>();
        loadingText.text = "Loading newspaper...";
        loadingText.fontSize = 22;
        loadingText.fontStyle = FontStyles.Italic;
        loadingText.alignment = TextAlignmentOptions.Center;
        loadingText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        loadingText.raycastTarget = false;
        loadGO.SetActive(false);

        newspaperPanel.SetActive(false);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }
}
