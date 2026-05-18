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
        new("Casey", "Hey! New hire, right? Don't worry, everyone has that look their first day."),
        new("Casey", "I'm Casey. I'll be showing you the ropes."),
        new("Casey", "We work in phases here. Pre-market is when you plan. Market hours are when things move. Post-market is when you see how you did. Then you rest. Simple rhythm."),
        new("Casey", "See your progression map? As you trade and learn, new concepts unlock. Some you study yourself. Others... find you, when you're ready."),
        new("Casey", "Don't overthink your first trade. It's never your best. That's the point."),
        new("Casey", "Alright, settle in. Get some rest — big day tomorrow."),
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

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Onboarding") return;
        StartCoroutine(RunOnboarding());
    }

    private IEnumerator RunOnboarding()
    {
        yield return null;

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
        else
        {
            Debug.LogWarning("[OnboardingController] No 'Casey' GameObject found in Onboarding scene — skipping dialogue.");
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
