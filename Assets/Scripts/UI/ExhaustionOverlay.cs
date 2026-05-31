using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ExhaustionOverlay : MonoBehaviour
{
    public static ExhaustionOverlay Inst { get; private set; }

    [SerializeField] private float fadeInDuration = 1f;
    [SerializeField] private float holdDuration = 2f;
    [SerializeField] private float fadeOutDuration = 1f;

    private GameObject overlayCanvas;
    private CanvasGroup canvasGroup;
    private TMP_Text messageText;
    private bool subscribedToTimer;
    private Coroutine activeSequence;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void OnDisable()
    {
        if (GamePhaseManager.Inst != null)
            GamePhaseManager.Inst.OnDayTimerExpired -= OnExhausted;
        subscribedToTimer = false;
    }

    void Update()
    {
        if (!subscribedToTimer)
            TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (subscribedToTimer) return;
        if (GamePhaseManager.Inst == null) return;
        GamePhaseManager.Inst.OnDayTimerExpired += OnExhausted;
        subscribedToTimer = true;
    }

    private void OnExhausted()
    {
        Show("I'm so exhausted... I should head home...");
    }

    public void Show(string message)
    {
        if (canvasGroup == null || activeSequence != null) return;
        messageText.text = message;
        overlayCanvas.SetActive(true);
        activeSequence = StartCoroutine(FadeSequence());
    }

    private IEnumerator FadeSequence()
    {
        canvasGroup.blocksRaycasts = true;
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(t / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        yield return new WaitForSeconds(holdDuration);

        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeOutDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        overlayCanvas.SetActive(false);
        activeSequence = null;
    }

    private void BuildUI()
    {
        overlayCanvas = new GameObject("ExhaustionCanvas");
        overlayCanvas.transform.SetParent(transform);
        var canvas = overlayCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;

        var scaler = overlayCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = overlayCanvas.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        var bg = new GameObject("Background");
        bg.transform.SetParent(overlayCanvas.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 1f);

        var textGO = new GameObject("Message");
        textGO.transform.SetParent(overlayCanvas.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(800, 100);
        textRect.anchoredPosition = Vector2.zero;
        messageText = textGO.AddComponent<TextMeshProUGUI>();
        messageText.fontSize = 28;
        messageText.fontStyle = FontStyles.Italic;
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.color = new Color(0.9f, 0.75f, 0.5f, 1f);
        messageText.raycastTarget = false;

        overlayCanvas.SetActive(false);
    }
}
