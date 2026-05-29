using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Inst { get; private set; }

    public string PendingSpawnPoint { get; private set; }

    [SerializeField] private float fadeDuration = 0.4f;

    private GameObject fadeCanvas;
    private CanvasGroup fadeGroup;
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

        // Fade to black
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeGroup.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
        fadeGroup.alpha = 1f;

        SceneManager.LoadScene(sceneName);

        // Wait a frame for the new scene to initialize
        yield return null;

        // Fade from black
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

    private void BuildFadeCanvas()
    {
        fadeCanvas = new GameObject("FadeCanvas");
        fadeCanvas.transform.SetParent(transform);
        var canvas = fadeCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        fadeCanvas.AddComponent<CanvasScaler>();
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

        fadeCanvas.SetActive(false);
    }
}
