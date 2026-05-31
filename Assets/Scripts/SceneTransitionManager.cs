using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Inst { get; private set; }

    public string PendingSpawnPoint { get; private set; }

    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private float minLoadingDisplay = 0.3f;

    private GameObject fadeCanvas;
    private CanvasGroup fadeGroup;
    private TMP_Text loadingText;
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

        string contextLine = BuildContextLine();
        if (!string.IsNullOrEmpty(contextLine))
            loadingText.text = contextLine;
        else
            loadingText.text = "Loading...";
        loadingText.gameObject.SetActive(true);

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

        loadingText.gameObject.SetActive(false);

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

    private static string BuildContextLine()
    {
        var gpm = GamePhaseManager.Inst;
        if (gpm == null) return null;

        string date = gpm.CurrentDate;
        if (string.IsNullOrEmpty(date)) return null;

        string arcName = gpm.CurrentArcDefinition?.name;
        if (!string.IsNullOrEmpty(arcName))
            return $"{arcName}  —  {FormatDate(date)}";

        return FormatDate(date);
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

        var bg = new GameObject("Black");
        bg.transform.SetParent(fadeCanvas.transform, false);
        var rt = bg.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        bg.AddComponent<Image>().color = Color.black;

        var textGO = new GameObject("LoadingText");
        textGO.transform.SetParent(fadeCanvas.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.5f, 0.3f);
        textRT.anchorMax = new Vector2(0.5f, 0.3f);
        textRT.sizeDelta = new Vector2(600, 50);
        textRT.anchoredPosition = Vector2.zero;
        loadingText = textGO.AddComponent<TextMeshProUGUI>();
        loadingText.fontSize = 18;
        loadingText.alignment = TextAlignmentOptions.Center;
        loadingText.color = new Color(0.5f, 0.5f, 0.55f, 1f);
        loadingText.text = "Loading...";
        loadingText.raycastTarget = false;
        textGO.SetActive(false);

        fadeCanvas.SetActive(false);
    }
}
