using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpeechBubble : MonoBehaviour
{
    [Header("Typewriter")]
    [SerializeField] private float charsPerSecond = 40f;

    [Header("Colors")]
    [SerializeField] private Color bgColor = new(0.06f, 0.06f, 0.1f, 0.92f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color promptColor = new(0.5f, 0.5f, 0.55f, 1f);

    private CanvasGroup canvasGroup;
    private TMP_Text speakerText;
    private TMP_Text dialogueText;
    private TMP_Text advancePrompt;
    private Transform followTarget;
    private Vector3 offset;
    private Camera mainCam;

    private bool isTyping;
    private int totalChars;
    private float charAccumulator;

    public bool IsTyping => isTyping;

    public void Init(Transform target, Vector3 worldOffset)
    {
        followTarget = target;
        offset = worldOffset;
        mainCam = Camera.main;
        BuildUI();
        canvasGroup.alpha = 0f;
    }

    void LateUpdate()
    {
        if (followTarget != null)
            transform.position = followTarget.position + offset;

        if (mainCam == null)
            mainCam = Camera.main;
        if (mainCam != null)
            transform.rotation = mainCam.transform.rotation;

        if (!isTyping) return;

        charAccumulator += Time.deltaTime * charsPerSecond;
        int visible = Mathf.Min(totalChars, (int)charAccumulator);
        dialogueText.maxVisibleCharacters = visible;

        if (visible >= totalChars)
        {
            isTyping = false;
            advancePrompt.gameObject.SetActive(true);
        }
    }

    public void Show(string speaker, string text, Color speakerColor)
    {
        speakerText.text = speaker;
        speakerText.color = speakerColor;
        dialogueText.text = text;
        dialogueText.ForceMeshUpdate();
        totalChars = dialogueText.textInfo.characterCount;
        dialogueText.maxVisibleCharacters = 0;
        charAccumulator = 0f;
        isTyping = true;
        advancePrompt.gameObject.SetActive(false);
        canvasGroup.alpha = 1f;
    }

    public void SkipTypewriter()
    {
        dialogueText.maxVisibleCharacters = totalChars;
        isTyping = false;
        advancePrompt.gameObject.SetActive(true);
    }

    public void Hide()
    {
        canvasGroup.alpha = 0f;
        isTyping = false;
    }

    private void BuildUI()
    {
        var canvasGO = new GameObject("BubbleCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = Vector3.zero;
        canvasGO.transform.localScale = Vector3.one * 0.005f;

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        var rt = canvasGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(520, 200);

        canvasGroup = canvasGO.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // Background
        var bg = new GameObject("BG");
        bg.transform.SetParent(canvasGO.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        Stretch(bgRect);
        bg.AddComponent<Image>().color = bgColor;

        // Speaker name — top strip
        speakerText = MakeText(bg, "Speaker", 24, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        var spRect = speakerText.rectTransform;
        spRect.anchorMin = new Vector2(0, 1);
        spRect.anchorMax = new Vector2(1, 1);
        spRect.pivot = new Vector2(0, 1);
        spRect.offsetMin = new Vector2(16, -36);
        spRect.offsetMax = new Vector2(-16, -8);

        // Dialogue — middle fill
        dialogueText = MakeText(bg, "Dialogue", 20, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        dialogueText.color = textColor;
        dialogueText.enableWordWrapping = true;
        var dlgRect = dialogueText.rectTransform;
        dlgRect.anchorMin = Vector2.zero;
        dlgRect.anchorMax = Vector2.one;
        dlgRect.offsetMin = new Vector2(16, 28);
        dlgRect.offsetMax = new Vector2(-16, -42);

        // Advance prompt — bottom right
        advancePrompt = MakeText(bg, "Prompt", 16, FontStyles.Normal, TextAlignmentOptions.BottomRight);
        advancePrompt.color = promptColor;
        advancePrompt.text = "E ▼";
        var prRect = advancePrompt.rectTransform;
        prRect.anchorMin = new Vector2(1, 0);
        prRect.anchorMax = new Vector2(1, 0);
        prRect.pivot = new Vector2(1, 0);
        prRect.anchoredPosition = new Vector2(-12, 6);
        prRect.sizeDelta = new Vector2(70, 22);
        advancePrompt.gameObject.SetActive(false);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    private static TMP_Text MakeText(GameObject parent, string name, float size,
        FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }
}
