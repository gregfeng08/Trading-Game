using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Threading.Tasks;
using TMPro;

public class TutorialController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TradingUIController tradingUI;

    [Header("Casey")]
    [SerializeField] private Vector3 bubbleOffset = new(0, 2.2f, 0);

    private SpeechBubble caseyBubble;
    private Transform caseyTransform;
    private bool skipRequested;
    private GameObject skipButtonCanvas;

    private string[] foundationalNodes;

    // VN-style dialogue overlay
    private GameObject vnCanvas;
    private GameObject vnPanel;
    private TMP_Text vnNameText;
    private TMP_Text vnBodyText;
    private TMP_Text vnPromptText;
    private bool vnTyping;
    private int vnTotalChars;
    private float vnCharAccum;
    private const float VnCharsPerSec = 45f;

    // ── Dialogue Lines ──

    private static readonly DialogueLine[] NarrativeIntro =
    {
        new("Casey", "Hey! New hire, right? I'm Casey — I'll be showing you the ropes around Hindsight Financial."),
        new("Casey", "We're a small trading firm. Your job is simple: buy and sell stocks to grow your portfolio. Do well, and the firm does well."),
        new("Casey", "Oh — it's 2007, by the way. The market's been on a tear. Everyone thinks it'll last forever. We'll see about that."),
    };

    private static readonly DialogueLine[] KnowledgeGraphIntro =
    {
        new("Casey", "Before we start trading, let me show you your Knowledge Graph."),
        new("Casey", "It tracks everything you learn here — concepts, strategies, lessons from your own trades. Think of it as your playbook."),
        new("Casey", "I'm opening it now. Read through the first concept and mark it complete. Then close the graph with Esc or the X button."),
    };

    private static readonly DialogueLine[] GraphSkippedLines =
    {
        new("Casey", "I'll show you the Knowledge Graph once you're set up in your room."),
    };

    private static readonly DialogueLine[] TradingIntro =
    {
        new("Casey", "Nice work. Now let's put that knowledge to use."),
        new("Casey", "I'm opening the trading terminal. Pick a stock, set how many shares you want, and hit Buy. Then hit Confirm Trades to send it to the market."),
    };

    private static readonly DialogueLine[] AdaptiveTriggered =
    {
        new("Casey", "Wait — did you see that? Your Knowledge Graph just updated."),
        new("Casey", "Your trade just triggered an adaptive node. See the orange one? Those unlock based on what you actually do in the market."),
        new("Casey", "Let me open the graph. Click the orange node and try \"Casey's Take\" — that's where I break down what just happened. Close the graph when you're done."),
    };

    private static readonly DialogueLine[] AdaptiveCompleted =
    {
        new("Casey", "Nice. As you trade more, new adaptive nodes will keep appearing — losses, timing, concentration. The graph watches everything."),
        new("Casey", "You don't need to go looking for them. They'll find you when the time is right."),
    };

    private static readonly DialogueLine[] ClosingLines =
    {
        new("Casey", "One more thing. At the end of each season, the firm reviews your performance — portfolio, trades, what you've learned."),
        new("Casey", "Do well and you'll get more buying power. Slack off and... well, let's not find out."),
        new("Casey", "I'll be keeping an eye on things. Head to your room — your trading terminal is there."),
        new("Casey", "Good luck out there. And remember — it's never your best trade that teaches you the most."),
    };

    // ── Public Entry Point ──

    public void StartTutorial()
    {
        StartCoroutine(RunTutorial());
    }

    private IEnumerator RunTutorial()
    {
        yield return null;

        SetupScene();
        BuildSkipButton();
        BuildVNPanel();

        yield return FetchFoundationalNodes();

        // 1. Introduction
        if (!skipRequested) yield return PlayDialogue(NarrativeIntro);

        // 2. Knowledge Graph — complete the first node
        if (!skipRequested) yield return PlayDialogue(KnowledgeGraphIntro);
        if (!skipRequested) yield return Step_KnowledgeGraph();

        // 3. Trading Terminal — first trade triggers market_buy_sell adaptive node
        if (!skipRequested) yield return PlayDialogue(TradingIntro);
        if (!skipRequested) yield return Step_FirstTrade();

        // 4. Adaptive node triggered — Casey explains, user completes it with Casey's Take
        if (!skipRequested) yield return PlayDialogue(AdaptiveTriggered);
        if (!skipRequested) yield return Step_CompleteAdaptiveNode();
        if (!skipRequested) yield return PlayDialogue(AdaptiveCompleted);

        // 5. Closing — seasonal reviews, Casey supervising, start game
        if (!skipRequested) yield return PlayDialogue(ClosingLines);

        // Transition to Room
        yield return Step_Transition();
    }

    // ── Setup ──

    private void SetupScene()
    {
        if (tradingUI == null)
            tradingUI = FindObjectOfType<TradingUIController>();

        EnsureKnowledgeGraphUI();

        var casey = GameObject.Find("Casey");
        if (casey != null)
        {
            caseyTransform = casey.transform;
            SetupCutsceneView(caseyTransform);
        }

        var bubbleGO = new GameObject("TutorialCaseyBubble");
        caseyBubble = bubbleGO.AddComponent<SpeechBubble>();
        if (caseyTransform != null)
            caseyBubble.Init(caseyTransform, bubbleOffset);
        else
            caseyBubble.Init(Camera.main?.transform ?? transform, Vector3.forward * 2f);

        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.CUTSCENE);
    }

    private void EnsureKnowledgeGraphUI()
    {
        if (KnowledgeGraphUI.Inst != null) return;

        KnowledgeGraphUIFactory.Build();
        Debug.Log("[Tutorial] Created KnowledgeGraphUI via factory for onboarding");
    }

    private void SetupCutsceneView(Transform casey)
    {
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 lookDir = cam.transform.position - casey.position;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.001f)
            casey.rotation = Quaternion.LookRotation(lookDir);

        var pm = FindObjectOfType<PlayerMovement>();
        if (pm != null)
        {
            Vector3 toCasey = casey.position - pm.transform.position;
            float yaw = Mathf.Atan2(toCasey.x, toCasey.z) * Mathf.Rad2Deg;
            pm.SetOrbitLocked(true, yaw, 10f, 4f);
        }
        else
        {
            cam.transform.LookAt(casey.position + Vector3.up * 1.2f);
        }
    }

    // ── VN-Style Dialogue Panel ──

    private void BuildVNPanel()
    {
        vnCanvas = new GameObject("VNDialogueCanvas");
        vnCanvas.transform.SetParent(transform);
        var canvas = vnCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        var scaler = vnCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        vnCanvas.AddComponent<GraphicRaycaster>();

        vnPanel = new GameObject("VNPanel", typeof(RectTransform), typeof(Image));
        vnPanel.transform.SetParent(vnCanvas.transform, false);
        vnPanel.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.1f, 0.95f);
        var rt = vnPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.05f, 0.02f);
        rt.anchorMax = new Vector2(0.95f, 0.20f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var nameGO = new GameObject("Name", typeof(RectTransform));
        nameGO.transform.SetParent(vnPanel.transform, false);
        var nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0f, 1f);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.pivot = new Vector2(0f, 1f);
        nameRT.anchoredPosition = new Vector2(20f, -8f);
        nameRT.sizeDelta = new Vector2(-40f, 26f);
        vnNameText = nameGO.AddComponent<TextMeshProUGUI>();
        vnNameText.text = "Casey";
        vnNameText.fontSize = UIConfig.Inst != null ? UIConfig.Inst.Scale(18f) : 18f;
        vnNameText.fontStyle = FontStyles.Bold;
        vnNameText.color = new Color(0.4f, 0.85f, 0.7f);
        vnNameText.raycastTarget = false;

        var bodyGO = new GameObject("Body", typeof(RectTransform));
        bodyGO.transform.SetParent(vnPanel.transform, false);
        var bodyRT = bodyGO.GetComponent<RectTransform>();
        bodyRT.anchorMin = Vector2.zero;
        bodyRT.anchorMax = Vector2.one;
        bodyRT.offsetMin = new Vector2(20f, 28f);
        bodyRT.offsetMax = new Vector2(-20f, -36f);
        vnBodyText = bodyGO.AddComponent<TextMeshProUGUI>();
        vnBodyText.fontSize = UIConfig.Inst != null ? UIConfig.Inst.Scale(16f) : 16f;
        vnBodyText.color = Color.white;
        vnBodyText.enableWordWrapping = true;
        vnBodyText.overflowMode = TextOverflowModes.Ellipsis;
        vnBodyText.raycastTarget = false;

        var promptGO = new GameObject("Prompt", typeof(RectTransform));
        promptGO.transform.SetParent(vnPanel.transform, false);
        var promptRT = promptGO.GetComponent<RectTransform>();
        promptRT.anchorMin = new Vector2(1f, 0f);
        promptRT.anchorMax = new Vector2(1f, 0f);
        promptRT.pivot = new Vector2(1f, 0f);
        promptRT.anchoredPosition = new Vector2(-16f, 6f);
        promptRT.sizeDelta = new Vector2(100f, 20f);
        vnPromptText = promptGO.AddComponent<TextMeshProUGUI>();
        vnPromptText.fontSize = UIConfig.Inst != null ? UIConfig.Inst.Scale(13f) : 13f;
        vnPromptText.color = new Color(0.5f, 0.5f, 0.55f);
        vnPromptText.alignment = TextAlignmentOptions.BottomRight;
        vnPromptText.raycastTarget = false;
        vnPromptText.text = "";

        vnPanel.SetActive(false);
    }

    private IEnumerator PlayVNDialogue(DialogueLine[] lines)
    {
        vnPanel.SetActive(true);
        vnPromptText.text = "";

        for (int i = 0; i < lines.Length; i++)
        {
            if (skipRequested) break;

            vnNameText.text = lines[i].speaker;
            vnNameText.color = lines[i].speaker == "Casey"
                ? new Color(0.4f, 0.85f, 0.7f)
                : Color.white;

            vnBodyText.text = lines[i].text;
            vnBodyText.ForceMeshUpdate();
            vnTotalChars = vnBodyText.textInfo.characterCount;
            vnBodyText.maxVisibleCharacters = 0;
            vnCharAccum = 0f;
            vnTyping = true;
            vnPromptText.text = "";

            while (vnTyping && !skipRequested)
            {
                if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
                {
                    vnBodyText.maxVisibleCharacters = vnTotalChars;
                    vnTyping = false;
                    break;
                }
                vnCharAccum += Time.deltaTime * VnCharsPerSec;
                int visible = Mathf.Min(vnTotalChars, (int)vnCharAccum);
                vnBodyText.maxVisibleCharacters = visible;
                if (visible >= vnTotalChars) vnTyping = false;
                yield return null;
            }

            vnPromptText.text = (i < lines.Length - 1) ? "E >" : "E to close";

            // Wait for input to advance
            yield return null; // consume the frame the click/key happened
            while (!skipRequested)
            {
                if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
                    break;
                yield return null;
            }
        }

        vnPanel.SetActive(false);
    }

    // ── Fetch Tutorial Config ──

    private IEnumerator FetchFoundationalNodes()
    {
        var task = Game.API.KnowledgeGraphAPI.GetTutorialConfig();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsCompletedSuccessfully && task.Result?.foundational_nodes != null)
        {
            foundationalNodes = task.Result.foundational_nodes;
            Debug.Log($"[Tutorial] Loaded {foundationalNodes.Length} foundational nodes from config");
        }
        else
        {
            foundationalNodes = new[] { "what_is_a_company", "what_is_a_stock" };
            Debug.LogWarning("[Tutorial] Failed to fetch tutorial config, using defaults");
        }
    }

    // ── Tutorial Steps ──

    private IEnumerator Step_KnowledgeGraph()
    {
        if (KnowledgeGraphUI.Inst == null)
        {
            yield return PlayDialogue(GraphSkippedLines);
            yield break;
        }

        float timeout = 5f;
        while (KnowledgeGraphManager.Inst != null && !KnowledgeGraphManager.Inst.IsInitialized && timeout > 0f)
        {
            yield return new WaitForSeconds(0.1f);
            timeout -= 0.1f;
        }

        string firstNode = foundationalNodes != null && foundationalNodes.Length > 0
            ? foundationalNodes[0] : "what_is_a_company";
        KnowledgeGraphUI.Inst.OpenToNode(firstNode);

        // Wait until user completes the node AND closes the graph themselves
        yield return new WaitUntil(() =>
            skipRequested ||
            (KnowledgeGraphManager.Inst != null && KnowledgeGraphManager.Inst.IsNodeCompleted(firstNode)));

        yield return new WaitUntil(() =>
            skipRequested || !KnowledgeGraphUI.Inst.IsOpen);

        if (skipRequested) yield break;

        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.CUTSCENE);

        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator Step_FirstTrade()
    {
        if (tradingUI == null)
        {
            Debug.LogWarning("[Tutorial] No TradingUIController — skipping first trade.");
            yield break;
        }

        tradingUI.TutorialMode = true;
        tradingUI.Open();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.TRADING);

        float timeout = 5f;
        while (!tradingUI.IsDataLoaded && timeout > 0f && !skipRequested)
        {
            yield return new WaitForSeconds(0.1f);
            timeout -= 0.1f;
        }
        if (!tradingUI.IsDataLoaded)
            tradingUI.ForceTutorialReady();

        if (skipRequested) yield break;

        yield return new WaitUntil(() => tradingUI.TutorialTradeConfirmed || skipRequested);

        // Record tutorial_first_trade event — triggers market_buy_sell adaptive node unlock
        if (KnowledgeGraphManager.Inst != null)
        {
            var task = RecordFirstTradeEvent();
            yield return new WaitUntil(() => task.IsCompleted);
        }

        yield return new WaitForSeconds(0.5f);

        tradingUI.Close();
        tradingUI.TutorialMode = false;

        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.CUTSCENE);

        yield return new WaitForSeconds(0.3f);
    }

    private async Task RecordFirstTradeEvent()
    {
        try
        {
            var req = new Game.API.DTO.PlayerEventRequestDTO
            {
                entity_id = Game.API.APIBootstrapper.EntityDbId,
                event_type = "tutorial_first_trade",
                metadata_json = null
            };
            await Game.API.PlayerEventsAPI.RecordEvent(req);
            await KnowledgeGraphManager.Inst.RefreshGraphAsync();
            Debug.Log("[Tutorial] Recorded tutorial_first_trade event, graph refreshed");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Tutorial] Failed to record first trade event: {ex.Message}");
        }
    }

    private IEnumerator Step_CompleteAdaptiveNode()
    {
        if (KnowledgeGraphUI.Inst == null || KnowledgeGraphManager.Inst == null)
            yield break;

        KnowledgeGraphUI.Inst.OpenToNode("market_buy_sell");

        // Wait for user to complete the node AND close the graph themselves
        yield return new WaitUntil(() =>
            skipRequested ||
            (KnowledgeGraphManager.Inst != null && KnowledgeGraphManager.Inst.IsNodeCompleted("market_buy_sell")));

        yield return new WaitUntil(() =>
            skipRequested || !KnowledgeGraphUI.Inst.IsOpen);

        if (skipRequested) yield break;

        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.CUTSCENE);

        yield return new WaitForSeconds(0.3f);
    }


    private IEnumerator Step_Transition()
    {
        Cleanup();

        if (SceneTransitionManager.Inst != null)
            SceneTransitionManager.Inst.LoadScene("Room", "bed");
        else
            SceneManager.LoadScene("Room");

        yield break;
    }

    // ── Dialogue Helpers ──

    private IEnumerator PlayDialogue(DialogueLine[] lines)
    {
        if (DialoguePlayer.Inst == null || caseyBubble == null) yield break;

        if (PlayerStateController.Inst != null
            && PlayerStateController.Inst.State != PlayerState.CUTSCENE)
            PlayerStateController.Inst.SetState(PlayerState.CUTSCENE);

        bool complete = false;
        DialoguePlayer.Inst.Play(lines, caseyBubble, () => complete = true);
        yield return new WaitUntil(() => complete || skipRequested);

        if (skipRequested && DialoguePlayer.Inst.IsPlaying)
            DialoguePlayer.Inst.ForceEnd();
    }

    // ── Skip Button ──

    private void BuildSkipButton()
    {
        var canvasGO = new GameObject("SkipTutorialCanvas");
        canvasGO.transform.SetParent(transform);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 160;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        var btnGO = new GameObject("SkipButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(canvasGO.transform, false);
        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(20f, 20f);
        rt.sizeDelta = new Vector2(140f, 36f);

        btnGO.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.7f);

        var btn = btnGO.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(OnSkipClicked);

        var textGO = new GameObject("Label", typeof(RectTransform));
        textGO.transform.SetParent(btnGO.transform, false);
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "Skip Tutorial";
        tmp.fontSize = 15;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.7f, 0.7f, 0.75f);
        tmp.raycastTarget = false;

        skipButtonCanvas = canvasGO;
    }

    private void OnSkipClicked()
    {
        if (skipRequested) return;
        skipRequested = true;

        if (DialoguePlayer.Inst != null && DialoguePlayer.Inst.IsPlaying)
            DialoguePlayer.Inst.ForceEnd();

        if (vnPanel != null) vnPanel.SetActive(false);

        if (KnowledgeGraphUI.Inst != null && KnowledgeGraphUI.Inst.IsOpen)
            KnowledgeGraphUI.Inst.Close();

        if (tradingUI != null)
        {
            tradingUI.Close();
            tradingUI.TutorialMode = false;
        }
    }

    // ── Cleanup ──

    private void Cleanup()
    {
        if (skipButtonCanvas != null)
            Destroy(skipButtonCanvas);

        if (vnCanvas != null)
            Destroy(vnCanvas);

        if (caseyBubble != null)
        {
            caseyBubble.Hide();
            Destroy(caseyBubble.gameObject);
            caseyBubble = null;
        }

        var pm = FindObjectOfType<PlayerMovement>();
        if (pm != null) pm.SetOrbitLocked(false);
    }
}
