using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ExhaustionOverlay : MonoBehaviour
{
    public static ExhaustionOverlay Inst { get; private set; }

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private float fadeInDuration = 1f;
    [SerializeField] private float holdDuration = 2f;
    [SerializeField] private float fadeOutDuration = 1f;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    void OnEnable()
    {
        if (GamePhaseManager.Inst != null)
            GamePhaseManager.Inst.OnDayTimerExpired += OnExhausted;
    }

    void OnDisable()
    {
        if (GamePhaseManager.Inst != null)
            GamePhaseManager.Inst.OnDayTimerExpired -= OnExhausted;
    }

    private void OnExhausted()
    {
        Show("I'm so exhausted... I should head home...");
    }

    public void Show(string message)
    {
        if (messageText != null)
            messageText.text = message;
        StartCoroutine(FadeSequence());
    }

    private IEnumerator FadeSequence()
    {
        if (canvasGroup == null) yield break;

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
    }
}
