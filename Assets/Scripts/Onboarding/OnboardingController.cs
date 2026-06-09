using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class OnboardingController : MonoBehaviour
{
    public static OnboardingController Inst { get; private set; }

    [Header("Casey")]
    [SerializeField] private Vector3 bubbleOffset = new(0, 2.2f, 0);

    private static readonly DialogueLine[] CaseyIntro =
    {
        new("Casey", "Hey! New hire, right? I'm Casey — I'll be showing you the ropes around Hindsight Financial."),
        new("Casey", "We work in phases here. Pre-market is when you plan your trades. Market hours are when prices move. Post-market is when you see how you did. Then you rest. Simple rhythm."),
        new("Casey", "The basics? A company sells pieces of itself — stocks — and you buy or sell them to try and profit. You'll pick it up fast."),
        new("Casey", "Your Knowledge Graph tracks what you learn here. New concepts unlock as you trade. Some you study yourself. Others... find you, when you're ready."),
        new("Casey", "Don't overthink your first trade. It's never your best one. That's the point."),
        new("Casey", "Head to your terminal and place your first trade. I'll be around if you need me."),
        new("Casey", "Oh — and welcome to 2007. Try not to break anything."),
    };

    private SpeechBubble activeBubble;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private bool onboardingCompleted;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Onboarding")
        {
            StartCoroutine(RunOnboarding());
            return;
        }

        if (scene.name == "Room" && !onboardingCompleted)
        {
            onboardingCompleted = true;
            _ = CleanupTutorialAndInit();
        }
    }

    private async System.Threading.Tasks.Task CleanupTutorialAndInit()
    {
        try
        {
            await Game.API.GameStateAPI.TutorialCleanup();
            Debug.Log("[Onboarding] Tutorial cleanup complete — portfolio and cash reset, knowledge preserved.");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Onboarding] Tutorial cleanup failed (may be first run): {ex.Message}");
        }
    }

    private async System.Threading.Tasks.Task AddFirstTradeObjective()
    {
        float timeout = 5f;
        while (KnowledgeGraphManager.Inst == null && timeout > 0f)
        {
            await System.Threading.Tasks.Task.Delay(100);
            timeout -= 0.1f;
        }
        if (KnowledgeGraphManager.Inst == null) return;

        if (!KnowledgeGraphManager.Inst.IsInitialized)
        {
            timeout = 5f;
            while (!KnowledgeGraphManager.Inst.IsInitialized && timeout > 0f)
            {
                await System.Threading.Tasks.Task.Delay(100);
                timeout -= 0.1f;
            }
        }

        if (KnowledgeGraphManager.Inst.IsNodeCompleted("market_buy_sell")) return;

        timeout = 3f;
        while (PlayerObjectivesUI.Inst == null && timeout > 0f)
        {
            await System.Threading.Tasks.Task.Delay(100);
            timeout -= 0.1f;
        }

        if (PlayerObjectivesUI.Inst != null)
            PlayerObjectivesUI.Inst.AddObjective("tutorial_first_trade", "Open the Trading Terminal and place your first trade");
    }

    private IEnumerator RunOnboarding()
    {
        yield return null;

        var tutorial = FindObjectOfType<TutorialController>();
        if (tutorial != null)
        {
            tutorial.StartTutorial();
            yield break;
        }

        // Fallback: no TutorialController in scene — run legacy intro
        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.CUTSCENE);

        var casey = GameObject.Find("Casey");
        if (casey != null)
        {
            SetupCutsceneView(casey.transform);

            var bubbleGO = new GameObject("CaseyBubble");
            activeBubble = bubbleGO.AddComponent<SpeechBubble>();
            activeBubble.Init(casey.transform, bubbleOffset);

            bool complete = false;
            DialoguePlayer.Inst.Play(CaseyIntro, activeBubble, () => complete = true);
            yield return new WaitUntil(() => complete);

            Destroy(activeBubble.gameObject);
            activeBubble = null;
        }

        var pm = FindObjectOfType<PlayerMovement>();
        if (pm != null) pm.SetOrbitLocked(false);

        if (SceneTransitionManager.Inst != null)
            SceneTransitionManager.Inst.LoadScene("Room", "bed");
        else
            SceneManager.LoadScene("Room");
    }

    private void SetupCutsceneView(Transform casey)
    {
        var cam = Camera.main;
        if (cam == null) return;

        // Face Casey toward the camera
        Vector3 lookDir = cam.transform.position - casey.position;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.001f)
            casey.transform.rotation = Quaternion.LookRotation(lookDir);

        // If player has orbit camera, lock it toward Casey for steady framing
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
}
