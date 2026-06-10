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
    private RectTransform bubbleRoot;
    private RectTransform bgRect;
    private GameObject speakerGO;
    private TMP_Text speakerText;
    private TMP_Text dialogueText;
    private GameObject promptGO;
    private TMP_Text advancePrompt;

    private Transform followTarget;
    private Vector3 offset;

    private bool isTyping;
    private int totalChars;
    private float charAccumulator;

    private bool isBark;
    private float barkTimer;

    private const float BubbleWidth = 340f;
    private const float Padding = 14f;
    private const float SpeakerHeight = 30f;
    private const float PromptHeight = 22f;
    private const float TailSize = 14f;
    private const float FadeDuration = 0.5f;

    public bool IsTyping => isTyping;
    public bool IsActive => canvasGroup != null && canvasGroup.alpha > 0f;

    public void Init(Transform target, Vector3 worldOffset)
    {
        followTarget = target;
        offset = worldOffset;
        BuildUI();
        canvasGroup.alpha = 0f;
    }

    void LateUpdate()
    {
        if (followTarget != null)
            transform.position = followTarget.position + offset;

        var cam = Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

        if (isTyping)
        {
            charAccumulator += Time.deltaTime * charsPerSecond;
            int visible = Mathf.Min(totalChars, (int)charAccumulator);
            dialogueText.maxVisibleCharacters = visible;

            if (visible >= totalChars)
            {
                isTyping = false;
                if (!isBark)
                    promptGO.SetActive(true);
            }
        }

        if (isBark && !isTyping && canvasGroup.alpha > 0f)
        {
            barkTimer -= Time.deltaTime;
            if (barkTimer <= FadeDuration)
                canvasGroup.alpha = Mathf.Clamp01(barkTimer / FadeDuration);
            if (barkTimer <= 0f)
            {
                canvasGroup.alpha = 0f;
                isBark = false;
            }
        }
    }

    public void Show(string speaker, string text, Color speakerColor)
    {
        isBark = false;
        speakerGO.SetActive(true);
        speakerText.text = speaker;
        speakerText.color = speakerColor;
        promptGO.SetActive(false);

        BeginTypewriter(text);
        ResizeBubble(true, true);
        canvasGroup.alpha = 1f;
    }

    public void ShowBark(string text, float duration = 4f)
    {
        isBark = true;
        barkTimer = duration;
        speakerGO.SetActive(false);
        promptGO.SetActive(false);

        BeginTypewriter(text);
        ResizeBubble(false, false);
        canvasGroup.alpha = 1f;
    }

    public void SkipTypewriter()
    {
        dialogueText.maxVisibleCharacters = totalChars;
        isTyping = false;
        if (!isBark)
            promptGO.SetActive(true);
    }

    public void SustainBark()
    {
        if (isBark)
            barkTimer = Mathf.Max(barkTimer, FadeDuration + 0.5f);
    }

    public void Hide()
    {
        canvasGroup.alpha = 0f;
        isTyping = false;
        isBark = false;
    }

    private void BeginTypewriter(string text)
    {
        dialogueText.text = text;
        dialogueText.ForceMeshUpdate();
        totalChars = dialogueText.textInfo.characterCount;
        dialogueText.maxVisibleCharacters = 0;
        charAccumulator = 0f;
        isTyping = true;
    }

    private void ResizeBubble(bool showSpeaker, bool showPrompt)
    {
        float contentWidth = BubbleWidth - 2f * Padding;
        float textHeight = dialogueText.GetPreferredValues(dialogueText.text, contentWidth, 0f).y;
        textHeight = Mathf.Max(textHeight, 24f);

        float totalHeight = Padding;
        if (showSpeaker) totalHeight += SpeakerHeight;
        totalHeight += textHeight;
        if (showPrompt) totalHeight += PromptHeight;
        totalHeight += Padding;

        bgRect.sizeDelta = new Vector2(BubbleWidth, totalHeight);
        bgRect.anchoredPosition = new Vector2(0f, TailSize * 0.35f);

        float y = -Padding;

        if (showSpeaker)
        {
            var spRect = speakerText.rectTransform;
            spRect.anchoredPosition = new Vector2(Padding, y);
            spRect.sizeDelta = new Vector2(contentWidth, SpeakerHeight);
            y -= SpeakerHeight;
        }

        var dlgRect = dialogueText.rectTransform;
        dlgRect.anchoredPosition = new Vector2(Padding, y);
        dlgRect.sizeDelta = new Vector2(contentWidth, textHeight);

        if (showPrompt)
        {
            var prRect = advancePrompt.rectTransform;
            prRect.anchoredPosition = new Vector2(-Padding, Padding * 0.5f);
        }

        bubbleRoot.sizeDelta = new Vector2(BubbleWidth, totalHeight + TailSize);
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

        bubbleRoot = canvasGO.GetComponent<RectTransform>();
        bubbleRoot.sizeDelta = new Vector2(BubbleWidth, 120f);
        bubbleRoot.pivot = new Vector2(0.5f, 0f);

        canvasGroup = canvasGO.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // Background panel
        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(canvasGO.transform, false);
        bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0f);
        bgRect.anchorMax = new Vector2(0.5f, 0f);
        bgRect.pivot = new Vector2(0.5f, 0f);
        bgRect.sizeDelta = new Vector2(BubbleWidth, 100f);
        bgRect.anchoredPosition = new Vector2(0f, TailSize * 0.35f);
        bgGO.AddComponent<Image>().color = bgColor;

        // Tail pointer
        var tailGO = new GameObject("Tail");
        tailGO.transform.SetParent(canvasGO.transform, false);
        var tailRect = tailGO.AddComponent<RectTransform>();
        tailRect.anchorMin = new Vector2(0.5f, 0f);
        tailRect.anchorMax = new Vector2(0.5f, 0f);
        tailRect.pivot = new Vector2(0.5f, 0.5f);
        tailRect.anchoredPosition = new Vector2(0f, TailSize * 0.25f);
        tailRect.sizeDelta = new Vector2(TailSize, TailSize);
        tailRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        tailGO.AddComponent<Image>().color = bgColor;

        // Speaker name (top of bg, hidden in bark mode)
        speakerGO = new GameObject("Speaker");
        speakerGO.transform.SetParent(bgGO.transform, false);
        var spRect = speakerGO.AddComponent<RectTransform>();
        spRect.anchorMin = new Vector2(0f, 1f);
        spRect.anchorMax = new Vector2(0f, 1f);
        spRect.pivot = new Vector2(0f, 1f);
        speakerText = speakerGO.AddComponent<TextMeshProUGUI>();
        speakerText.fontSize = 22;
        speakerText.fontStyle = FontStyles.Bold;
        speakerText.alignment = TextAlignmentOptions.TopLeft;
        speakerText.enableWordWrapping = false;
        speakerText.overflowMode = TextOverflowModes.Ellipsis;
        speakerText.raycastTarget = false;

        // Dialogue text (middle area)
        var dlgGO = new GameObject("Dialogue");
        dlgGO.transform.SetParent(bgGO.transform, false);
        var dlgRect = dlgGO.AddComponent<RectTransform>();
        dlgRect.anchorMin = new Vector2(0f, 1f);
        dlgRect.anchorMax = new Vector2(0f, 1f);
        dlgRect.pivot = new Vector2(0f, 1f);
        dialogueText = dlgGO.AddComponent<TextMeshProUGUI>();
        dialogueText.fontSize = 18;
        dialogueText.color = textColor;
        dialogueText.alignment = TextAlignmentOptions.TopLeft;
        dialogueText.enableWordWrapping = true;
        dialogueText.overflowMode = TextOverflowModes.Overflow;
        dialogueText.raycastTarget = false;

        // Advance prompt (bottom-right, hidden in bark mode)
        promptGO = new GameObject("Prompt");
        promptGO.transform.SetParent(bgGO.transform, false);
        var prRect = promptGO.AddComponent<RectTransform>();
        prRect.anchorMin = new Vector2(1f, 0f);
        prRect.anchorMax = new Vector2(1f, 0f);
        prRect.pivot = new Vector2(1f, 0f);
        advancePrompt = promptGO.AddComponent<TextMeshProUGUI>();
        advancePrompt.fontSize = 14;
        advancePrompt.alignment = TextAlignmentOptions.BottomRight;
        advancePrompt.color = promptColor;
        advancePrompt.text = "E >";
        advancePrompt.raycastTarget = false;
        promptGO.SetActive(false);
    }
}
