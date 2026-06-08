using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using Game.API;
using Game.API.DTO;

public class ArcTransitionOverlay : MonoBehaviour
{
    public static ArcTransitionOverlay Inst { get; private set; }

    [Header("Timing")]
    [SerializeField] private float fadeInDuration = 1.2f;
    [SerializeField] private float staggerDelay = 0.6f;
    [SerializeField] private float fadeOutDuration = 0.8f;

    [Header("Colors")]
    [SerializeField] private Color backgroundColor = new(0.02f, 0.02f, 0.05f, 1f);
    [SerializeField] private Color headerColor = new(0.85f, 0.75f, 0.45f, 1f);
    [SerializeField] private Color bodyColor = new(0.72f, 0.72f, 0.76f, 1f);
    [SerializeField] private Color flavorColor = new(0.55f, 0.6f, 0.75f, 1f);
    [SerializeField] private Color dimColor = new(0.4f, 0.4f, 0.45f, 1f);
    [SerializeField] private Color positiveColor = new(0.4f, 0.9f, 0.4f, 1f);
    [SerializeField] private Color negativeColor = new(0.9f, 0.4f, 0.4f, 1f);

    private GameObject overlayCanvas;
    private CanvasGroup canvasGroup;
    private TMP_Text headerText;
    private TMP_Text arcNameText;
    private TMP_Text gradeText;
    private TMP_Text returnText;
    private TMP_Text cashText;
    private GameObject dividerObj;
    private TMP_Text nextHeaderText;
    private TMP_Text nextNameText;
    private TMP_Text descText;
    private TMP_Text flavorTextEl;
    private TMP_Text continueText;

    private bool waitingForInput;
    private bool skipRequested;
    private float showStartTime;
    private Coroutine activeSequence;
    private bool subscribedToArcTransition;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        TrySubscribe();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (GamePhaseManager.Inst != null)
            GamePhaseManager.Inst.OnArcTransition -= ShowTransition;
        subscribedToArcTransition = false;
    }

    void Update()
    {
        if (!subscribedToArcTransition)
            TrySubscribe();

        if (waitingForInput && Input.anyKeyDown)
            waitingForInput = false;
        else if (!waitingForInput && activeSequence != null && Input.anyKeyDown
                 && Time.time - showStartTime > fadeInDuration + 0.5f)
            skipRequested = true;

        if (activeSequence == null && overlayCanvas != null && overlayCanvas.activeSelf)
        {
            Debug.LogWarning("[ArcTransitionOverlay] Overlay stuck active with no running sequence — forcing cleanup.");
            Cleanup();
        }
    }

    private IEnumerator Stagger()
    {
        if (skipRequested) yield break;
        float t = 0f;
        while (t < staggerDelay && !skipRequested)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    private void TrySubscribe()
    {
        if (subscribedToArcTransition) return;
        if (GamePhaseManager.Inst == null) return;
        GamePhaseManager.Inst.OnArcTransition += ShowTransition;
        subscribedToArcTransition = true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool wasActive = activeSequence != null;
        if (wasActive) { StopCoroutine(activeSequence); activeSequence = null; }
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        if (overlayCanvas != null) overlayCanvas.SetActive(false);
        if (wasActive) UnlockPlayer();

        if (scene.name == "Onboarding" || scene.name == "Main Menu") return;
        var gpm = GamePhaseManager.Inst;
        if (gpm == null || !gpm.PendingArcIntro || gpm.CurrentArcDefinition == null) return;
        gpm.PendingArcIntro = false;
        ShowIntro(gpm.CurrentArcDefinition);
    }

    public void ShowTransition(ArcTransitionDTO transition)
    {
        if (activeSequence != null) StopCoroutine(activeSequence);
        skipRequested = false;
        showStartTime = Time.time;
        activeSequence = StartCoroutine(TransitionSequence(transition));
    }

    public void ShowIntro(ArcDefinitionDTO arc)
    {
        if (activeSequence != null) StopCoroutine(activeSequence);
        skipRequested = false;
        showStartTime = Time.time;
        activeSequence = StartCoroutine(IntroSequence(arc));
    }

    private IEnumerator IntroSequence(ArcDefinitionDTO arc)
    {
        if (arc == null)
        {
            Debug.LogWarning("[ArcTransitionOverlay] IntroSequence called with null arc.");
            yield break;
        }

        overlayCanvas.SetActive(true);
        LockPlayer();
        HideAll();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        double cash = 10000;
        var portfolioTask = APIBootstrapper.EntityDbId > 0
            ? TradeAPI.GetPortfolio(APIBootstrapper.EntityDbId)
            : null;

        yield return new WaitForSeconds(0.3f);

        if (portfolioTask != null)
        {
            if (!portfolioTask.IsCompleted)
                yield return new WaitUntil(() => portfolioTask.IsCompleted);
            if (portfolioTask.IsCompletedSuccessfully && portfolioTask.Result?.entity != null)
                cash = portfolioTask.Result.entity.available_cash;
        }

        string arcNum = ArcNumber(arc.id);
        SetText(headerText, $"ARC {arcNum}", dimColor);
        arcNameText.fontSize = 44;
        SetText(arcNameText, (arc.name ?? "").ToUpper(), headerColor);
        yield return Stagger();

        SetText(returnText, FormatDateRange(arc.start_date, arc.end_date), dimColor);
        SetText(cashText, $"Starting Cash:  ${cash:N0}", bodyColor);
        yield return Stagger();

        dividerObj.SetActive(true);
        string objective = GetArcObjective(arc.id ?? "");
        SetText(descText, objective, bodyColor);
        yield return Stagger();

        SetText(continueText, "Press any key to continue", dimColor);
        waitingForInput = true;
        yield return new WaitUntil(() => !waitingForInput);

        yield return Fade(1f, 0f, fadeOutDuration);
        Cleanup();
    }

    private IEnumerator TransitionSequence(ArcTransitionDTO t)
    {
        overlayCanvas.SetActive(true);
        LockPlayer();
        HideAll();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        yield return new WaitForSeconds(0.3f);

        var c = t.completed_arc;

        SetText(headerText, "ARC COMPLETE", headerColor);
        arcNameText.fontSize = 36;
        SetText(arcNameText, c.arc_name, Color.white);
        yield return Stagger();

        SetText(gradeText, c.grade, GradeColor(c.grade));
        SetText(returnText, $"{c.return_pct:+0.0;-0.0}% return", bodyColor);
        yield return Stagger();

        double delta = t.cash_after - t.cash_before;
        string sign = delta >= 0 ? "+" : "";
        SetText(cashText,
            $"${t.cash_before:N0}  →  ${t.cash_after:N0}  ({sign}{delta:N0})",
            delta >= 0 ? positiveColor : negativeColor);
        yield return Stagger();

        if (t.next_arc != null)
        {
            dividerObj.SetActive(true);
            SetText(nextHeaderText, "NEXT ARC", headerColor);
            SetText(nextNameText, t.next_arc.name, Color.white);
            SetText(descText, t.next_arc.description, bodyColor);
            yield return Stagger();
        }

        SetText(continueText, "Press any key to continue", dimColor);
        waitingForInput = true;
        yield return new WaitUntil(() => !waitingForInput);

        yield return Fade(1f, 0f, fadeOutDuration);
        Cleanup();
    }

    private void Cleanup()
    {
        canvasGroup.blocksRaycasts = false;
        HideAll();
        UnlockPlayer();
        activeSequence = null;
        overlayCanvas.SetActive(false);
    }

    private void LockPlayer()
    {
        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.CUTSCENE);
    }

    private void UnlockPlayer()
    {
        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.MOVING);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    private void HideAll()
    {
        headerText.gameObject.SetActive(false);
        arcNameText.gameObject.SetActive(false);
        gradeText.gameObject.SetActive(false);
        returnText.gameObject.SetActive(false);
        cashText.gameObject.SetActive(false);
        dividerObj.SetActive(false);
        nextHeaderText.gameObject.SetActive(false);
        nextNameText.gameObject.SetActive(false);
        descText.gameObject.SetActive(false);
        flavorTextEl.gameObject.SetActive(false);
        continueText.gameObject.SetActive(false);
    }

    private static void SetText(TMP_Text el, string text, Color color)
    {
        el.text = text;
        el.color = color;
        el.gameObject.SetActive(true);
    }

    private static Color GradeColor(string grade)
    {
        return grade switch
        {
            "S" => new Color(1f, 0.84f, 0f),
            "A" => new Color(0.4f, 0.9f, 0.4f),
            "B" => new Color(0.5f, 0.8f, 1f),
            "C" => new Color(0.9f, 0.9f, 0.5f),
            "D" => new Color(0.9f, 0.6f, 0.3f),
            _ => new Color(0.9f, 0.4f, 0.4f),
        };
    }

    private static string ArcNumber(string arcId)
    {
        return arcId switch
        {
            "bull_market" => "I",
            "the_unraveling" => "II",
            "meltdown" => "III",
            _ => ""
        };
    }

    private static string FormatDateRange(string start, string end)
    {
        string s = FormatDate(start);
        string e = FormatDate(end);
        if (s != null && e != null) return $"{s}  —  {e}";
        return s ?? "";
    }

    private static string FormatDate(string isoDate)
    {
        if (string.IsNullOrEmpty(isoDate)) return null;
        if (System.DateTime.TryParse(isoDate, out var dt))
            return dt.ToString("MMMM d, yyyy");
        return isoDate;
    }

    private static string GetArcObjective(string arcId)
    {
        return arcId switch
        {
            "bull_market" =>
                "Objective: Grow your portfolio.\n" +
                "The market is rising — learn the basics, place trades, and try to beat +10% return for a top grade.\n\n" +
                "Tip: Don't put all your money in one stock. Diversify early.",
            "the_unraveling" =>
                "Objective: Read the signs.\n" +
                "The easy gains are over. Staying above +5% earns a top grade — but staying positive at all is respectable.\n\n" +
                "Tip: Watch your cash reserves. Opportunities appear when others are panicking.",
            "meltdown" =>
                "Objective: Survive.\n" +
                "Everything is falling. Breaking even earns the highest grade here. Losing less than others is winning.\n\n" +
                "Tip: Cash is king. Sometimes the best trade is no trade at all.",
            _ => "Trade wisely. Your grade is based on portfolio return."
        };
    }

    private static string GetFlavorText(string arcId)
    {
        return arcId switch
        {
            "bull_market" =>
                "“A perfect time to learn. Just don’t mistake a rising tide\n" +
                "for swimming skill.”\n\n" +
                "— The Principal",
            "the_unraveling" =>
                "“Everyone has a plan until the market\n" +
                "punches them in the face.”\n\n" +
                "— The Principal",
            "meltdown" =>
                "“Survival is its own kind of victory.\n" +
                "Remember that when the numbers turn red.”\n\n" +
                "— The Principal",
            _ => null
        };
    }

    // ── UI Construction ──

    private void BuildUI()
    {
        var canvasGO = new GameObject("ArcOverlayCanvas");
        overlayCanvas = canvasGO;
        canvasGO.transform.SetParent(transform);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        canvasGroup = canvasGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        var bg = new GameObject("Background");
        bg.transform.SetParent(canvasGO.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        Stretch(bgRect);
        bg.AddComponent<Image>().color = backgroundColor;

        var content = new GameObject("Content");
        content.transform.SetParent(bg.transform, false);
        var contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(800, 0);
        contentRect.anchoredPosition = Vector2.zero;

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 14;
        vlg.padding = new RectOffset(40, 40, 20, 20);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        headerText    = MakeText(content, "Header",      30, FontStyles.Bold,   TextAlignmentOptions.Center);
        arcNameText   = MakeText(content, "ArcName",     36, FontStyles.Bold,   TextAlignmentOptions.Center);
        gradeText     = MakeText(content, "Grade",       80, FontStyles.Bold,   TextAlignmentOptions.Center);
        returnText    = MakeText(content, "Return",      22, FontStyles.Normal,  TextAlignmentOptions.Center);
        cashText      = MakeText(content, "Cash",        20, FontStyles.Normal,  TextAlignmentOptions.Center);
        dividerObj    = MakeDivider(content);
        nextHeaderText = MakeText(content, "NextHeader", 26, FontStyles.Bold,   TextAlignmentOptions.Center);
        nextNameText  = MakeText(content, "NextName",    30, FontStyles.Bold,   TextAlignmentOptions.Center);
        descText      = MakeText(content, "Description", 20, FontStyles.Normal,  TextAlignmentOptions.Center);
        flavorTextEl  = MakeText(content, "Flavor",      18, FontStyles.Italic,  TextAlignmentOptions.Center);
        MakeSpacer(content, 24);
        continueText  = MakeText(content, "Continue",    16, FontStyles.Normal,  TextAlignmentOptions.Center);

        HideAll();
        overlayCanvas.SetActive(false);
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

    private static GameObject MakeDivider(GameObject parent)
    {
        var go = new GameObject("Divider");
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 1;
        le.flexibleWidth = 1;
        return go;
    }

    private static void MakeSpacer(GameObject parent, float height)
    {
        var go = new GameObject("Spacer");
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
    }
}
