using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Inst { get; private set; }

    public string PendingSpawnPoint { get; private set; }

    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float minLoadingDisplay = 1.8f;

    private GameObject fadeCanvas;
    private CanvasGroup fadeGroup;
    private TMP_Text loadingText;
    private TMP_Text arcTitleText;
    private TMP_Text arcDescText;
    private TMP_Text brandText;
    private bool isTransitioning;

    private Coroutine activeFade;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);
        BuildFadeCanvas();

        if (ExhaustionOverlay.Inst == null)
        {
            var go = new GameObject("ExhaustionOverlay");
            go.AddComponent<ExhaustionOverlay>();
        }
    }

    public void LoadScene(string sceneName, string spawnPoint = null)
    {
        PendingSpawnPoint = spawnPoint;

        if (isTransitioning)
        {
            if (activeFade != null)
            {
                StopCoroutine(activeFade);
                activeFade = null;
            }
            isTransitioning = false;
        }

        activeFade = StartCoroutine(FadeTransition(sceneName));
    }

    public void ConsumePendingSpawnPoint()
    {
        PendingSpawnPoint = null;
    }

    private IEnumerator FadeTransition(string sceneName)
    {
        isTransitioning = true;
        fadeCanvas.SetActive(true);
        loadingText.gameObject.SetActive(false);

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeGroup.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
        fadeGroup.alpha = 1f;

        ShowLoadingContent();

        var asyncOp = SceneManager.LoadSceneAsync(sceneName);
        asyncOp.allowSceneActivation = false;

        float loadStart = Time.unscaledTime;

        while (asyncOp.progress < 0.9f)
            yield return null;

        float elapsed = Time.unscaledTime - loadStart;
        if (elapsed < minLoadingDisplay)
            yield return new WaitForSecondsRealtime(minLoadingDisplay - elapsed);

        asyncOp.allowSceneActivation = true;

        yield return null;

        HideLoadingContent();

        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeGroup.alpha = 1f - Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
        fadeGroup.alpha = 0f;
        fadeCanvas.SetActive(false);
        isTransitioning = false;
        activeFade = null;
    }

    private void ShowLoadingContent()
    {
        var gpm = GamePhaseManager.Inst;
        var arc = gpm?.CurrentArcDefinition;

        if (arc != null && !string.IsNullOrEmpty(arc.name))
        {
            arcTitleText.text = arc.name.ToUpper();
            arcTitleText.gameObject.SetActive(true);

            if (!string.IsNullOrEmpty(arc.description))
            {
                arcDescText.text = arc.description;
                arcDescText.gameObject.SetActive(true);
            }

            string date = gpm.CurrentDate;
            loadingText.text = !string.IsNullOrEmpty(date) ? FormatDate(date) : "";
            loadingText.gameObject.SetActive(true);
        }
        else
        {
            arcTitleText.gameObject.SetActive(false);
            arcDescText.gameObject.SetActive(false);
            loadingText.text = "Loading...";
            loadingText.gameObject.SetActive(true);
        }

        brandText.gameObject.SetActive(true);
    }

    private void HideLoadingContent()
    {
        loadingText.gameObject.SetActive(false);
        arcTitleText.gameObject.SetActive(false);
        arcDescText.gameObject.SetActive(false);
        brandText.gameObject.SetActive(false);
    }

    private static string FormatDate(string isoDate)
    {
        if (System.DateTime.TryParse(isoDate, out var dt))
            return dt.ToString("MMMM d, yyyy");
        return isoDate;
    }

    private void BuildFadeCanvas()
    {
        fadeCanvas = new GameObject("FadeCanvas");
        fadeCanvas.transform.SetParent(transform);
        var canvas = fadeCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        var scaler = fadeCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        fadeGroup = fadeCanvas.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;

        // Background
        var bg = new GameObject("Black");
        bg.transform.SetParent(fadeCanvas.transform, false);
        var rt = bg.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.04f, 1f);

        // Arc title — large, centered, upper area
        var titleGO = new GameObject("ArcTitle");
        titleGO.transform.SetParent(fadeCanvas.transform, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 0.6f);
        titleRT.anchorMax = new Vector2(0.5f, 0.6f);
        titleRT.sizeDelta = new Vector2(800, 60);
        titleRT.anchoredPosition = Vector2.zero;
        arcTitleText = titleGO.AddComponent<TextMeshProUGUI>();
        arcTitleText.fontSize = 36;
        arcTitleText.fontStyle = FontStyles.Bold;
        arcTitleText.alignment = TextAlignmentOptions.Center;
        arcTitleText.color = new Color(0.85f, 0.75f, 0.45f, 1f);
        arcTitleText.characterSpacing = 8f;
        arcTitleText.raycastTarget = false;
        titleGO.SetActive(false);

        // Arc description — atmospheric flavor text
        var descGO = new GameObject("ArcDesc");
        descGO.transform.SetParent(fadeCanvas.transform, false);
        var descRT = descGO.AddComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0.5f, 0.45f);
        descRT.anchorMax = new Vector2(0.5f, 0.45f);
        descRT.sizeDelta = new Vector2(700, 100);
        descRT.anchoredPosition = Vector2.zero;
        arcDescText = descGO.AddComponent<TextMeshProUGUI>();
        arcDescText.fontSize = 16;
        arcDescText.fontStyle = FontStyles.Italic;
        arcDescText.alignment = TextAlignmentOptions.Center;
        arcDescText.color = new Color(0.6f, 0.6f, 0.65f, 1f);
        arcDescText.enableWordWrapping = true;
        arcDescText.raycastTarget = false;
        descGO.SetActive(false);

        // Date — below the description
        var textGO = new GameObject("DateText");
        textGO.transform.SetParent(fadeCanvas.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.5f, 0.32f);
        textRT.anchorMax = new Vector2(0.5f, 0.32f);
        textRT.sizeDelta = new Vector2(600, 40);
        textRT.anchoredPosition = Vector2.zero;
        loadingText = textGO.AddComponent<TextMeshProUGUI>();
        loadingText.fontSize = 18;
        loadingText.alignment = TextAlignmentOptions.Center;
        loadingText.color = new Color(0.5f, 0.5f, 0.55f, 1f);
        loadingText.text = "Loading...";
        loadingText.raycastTarget = false;
        textGO.SetActive(false);

        // Brand watermark — bottom center
        var brandGO = new GameObject("Brand");
        brandGO.transform.SetParent(fadeCanvas.transform, false);
        var brandRT = brandGO.AddComponent<RectTransform>();
        brandRT.anchorMin = new Vector2(0.5f, 0.08f);
        brandRT.anchorMax = new Vector2(0.5f, 0.08f);
        brandRT.sizeDelta = new Vector2(400, 30);
        brandRT.anchoredPosition = Vector2.zero;
        brandText = brandGO.AddComponent<TextMeshProUGUI>();
        brandText.fontSize = 12;
        brandText.alignment = TextAlignmentOptions.Center;
        brandText.color = new Color(0.35f, 0.35f, 0.4f, 0.6f);
        brandText.text = "HINDSIGHT FINANCIAL";
        brandText.characterSpacing = 6f;
        brandText.raycastTarget = false;
        brandGO.SetActive(false);

        fadeCanvas.SetActive(false);
    }
}
