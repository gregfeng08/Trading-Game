using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class OptionsMenuUI : MonoBehaviour
{
    public static OptionsMenuUI Inst { get; private set; }

    [Header("Panel")]
    [SerializeField] private GameObject panel;

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button mainMenuButton;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);

        if (panel != null && panel != gameObject)
            panel.SetActive(false);
        else if (panel == gameObject)
            Debug.LogWarning("[OptionsMenuUI] 'panel' references this GameObject — assign a child container instead, or Escape detection won't work while panel is hidden.");

        if (resumeButton != null)
            resumeButton.onClick.AddListener(Close);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (panel != null && panel.activeSelf)
            Close();
    }

    void Update()
    {
        if (SceneManager.GetActiveScene().name == "Main Menu") return;

        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        Debug.Log($"[OptionsMenuUI] Escape pressed. panel={panel != null}, panel.active={panel?.activeSelf}, PSC={PlayerStateController.Inst != null}, state={PlayerStateController.Inst?.State}");

        if (panel != null && panel.activeSelf)
        {
            Close();
            return;
        }

        if (PlayerStateController.Inst == null) return;
        var state = PlayerStateController.Inst.State;
        if (state == PlayerState.MOVING || state == PlayerState.CUTSCENE)
            Open();
    }

    public void Open()
    {
        if (panel == null) return;
        panel.SetActive(true);

        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.PAUSED);

        Time.timeScale = 0f;
    }

    public void Close()
    {
        if (panel == null) return;
        panel.SetActive(false);

        Time.timeScale = 1f;

        if (PlayerStateController.Inst != null && PlayerStateController.Inst.State == PlayerState.PAUSED)
            PlayerStateController.Inst.SetState(PlayerState.MOVING);
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Main Menu");
    }
}
