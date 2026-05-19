using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;
using Game.API;
using Game.API.DTO;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject playButtonObj;
    [SerializeField] private GameObject newGameButtonObj;
    [SerializeField] private GameObject resetButtonObj;

    [Header("Bootstrap")]
    [SerializeField] private APIBootstrapper bootstrapper;

    private Coroutine _poll;
    private Button playButton;
    private Button newGameButton;
    private Button resetButton;

    private void Awake()
    {
        playButton = playButtonObj.GetComponent<Button>();
        newGameButton = newGameButtonObj.GetComponent<Button>();
        resetButton = resetButtonObj.GetComponent<Button>();
    }

    private void OnEnable()
    {
        playButtonObj.SetActive(false);
        newGameButtonObj.SetActive(false);
        resetButtonObj.SetActive(false);

        bootstrapper.TryStart();
        _poll = StartCoroutine(PollBootstrap());
    }

    private void OnDisable()
    {
        if (_poll != null) StopCoroutine(_poll);
        _poll = null;
    }

    private IEnumerator PollBootstrap()
    {
        while (true)
        {
            switch (APIBootstrapper.State)
            {
                case APIBootstrapper.BootState.NotStarted:
                    statusText.text = "Starting backend...";
                    bootstrapper.TryStart();
                    break;

                case APIBootstrapper.BootState.Bootstrapping:
                    statusText.text = "Connecting...";
                    break;

                case APIBootstrapper.BootState.Ready:
                    statusText.text = "Server Ready";
                    StartCoroutine(CheckSaveAndShowButtons());
                    yield break;

                case APIBootstrapper.BootState.Failed:
                    statusText.text = "Failed to connect. Retrying...";
                    APIBootstrapper.ResetForRetry();
                    yield return new WaitForSeconds(1.0f);
                    bootstrapper.TryStart();
                    break;
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    private IEnumerator CheckSaveAndShowButtons()
    {
        var task = GameStateAPI.GetGameDate();
        yield return new WaitUntil(() => task.IsCompleted);

        bool hasSave = false;
        if (task.IsCompletedSuccessfully && task.Result.current_date != null)
            hasSave = true;

        if (hasSave)
        {
            playButtonObj.SetActive(true);
            playButton.onClick.AddListener(OnPlay);
        }

        newGameButtonObj.SetActive(true);
        newGameButton.onClick.AddListener(OnNewGame);

        resetButtonObj.SetActive(true);
        resetButton.onClick.AddListener(OnReset);

        statusText.text = hasSave ? "Welcome back" : "Ready to start";
    }

    private void OnPlay()
    {
        SetAllButtonsInteractable(false);
        LoadRoom();
    }

    private void OnNewGame()
    {
        SetAllButtonsInteractable(false);
        StartCoroutine(NewGameFlow());
    }

    private void OnReset()
    {
        SetAllButtonsInteractable(false);
        StartCoroutine(ResetFlow());
    }

    private IEnumerator NewGameFlow()
    {
        statusText.text = "Creating new game...";

        var config = bootstrapper.GetConfig();
        var startDate = config != null ? config.gameStartDate : "2007-04-02";

        var task = GameStateAPI.NewGame(startDate);
        yield return new WaitUntil(() => task.IsCompleted);

        if (!task.IsCompletedSuccessfully)
        {
            statusText.text = "Failed to create game. Try again.";
            SetAllButtonsInteractable(true);
            yield break;
        }

        if (config != null && config.registerEntity)
        {
            var entity = new EntityDTO
            {
                entity_id = config.entity_id,
                entity_type = config.entity_type,
                display_name = config.display_name
            };
            var regTask = DbAPI.RegisterEntity(entity);
            yield return new WaitUntil(() => regTask.IsCompleted);

            if (regTask.IsCompletedSuccessfully)
            {
                APIBootstrapper.EntityDbId = regTask.Result.entity_db_id;
                APIBootstrapper.EntityExternalId = config.entity_id;
            }
        }

        if (KnowledgeGraphManager.Inst != null)
        {
            var kgTask = KnowledgeGraphManager.Inst.InitializeAsync();
            yield return new WaitUntil(() => kgTask.IsCompleted);
        }

        if (GamePhaseManager.Inst != null)
        {
            var syncTask = GamePhaseManager.Inst.SyncWithServer();
            yield return new WaitUntil(() => syncTask.IsCompleted);
            GamePhaseManager.Inst.PendingArcIntro = true;
        }

        LoadOnboarding();
    }

    private IEnumerator ResetFlow()
    {
        statusText.text = "Resetting database...";

        var resetTask = DbAPI.ResetDB();
        yield return new WaitUntil(() => resetTask.IsCompleted);

        if (!resetTask.IsCompletedSuccessfully || resetTask.Result.status != "ok")
        {
            statusText.text = "Reset failed. Try again.";
            SetAllButtonsInteractable(true);
            yield break;
        }

        statusText.text = "Initializing database...";

        var initTask = DbAPI.InitializeDB();
        yield return new WaitUntil(() => initTask.IsCompleted);

        if (!initTask.IsCompletedSuccessfully)
        {
            statusText.text = "Init failed. Try again.";
            SetAllButtonsInteractable(true);
            yield break;
        }

        statusText.text = "Downloading market data...";

        var config = bootstrapper.GetConfig();
        var loadTask = DbAPI.LoadTickers(
            config != null ? config.startDate : "2005-01-01",
            config != null ? config.endDate : "2010-12-31",
            config != null ? config.topN : 50
        );
        yield return new WaitUntil(() => loadTask.IsCompleted);

        if (!loadTask.IsCompletedSuccessfully || loadTask.Result.status != "ok")
        {
            statusText.text = "Ticker download failed. Try again.";
            SetAllButtonsInteractable(true);
            yield break;
        }

        if (config != null && config.registerEntity)
        {
            statusText.text = "Registering player...";

            var entity = new EntityDTO
            {
                entity_id = config.entity_id,
                entity_type = config.entity_type,
                display_name = config.display_name
            };
            var regTask = DbAPI.RegisterEntity(entity);
            yield return new WaitUntil(() => regTask.IsCompleted);

            if (regTask.IsCompletedSuccessfully)
            {
                APIBootstrapper.EntityDbId = regTask.Result.entity_db_id;
                APIBootstrapper.EntityExternalId = config.entity_id;
            }
        }

        statusText.text = "Reset complete!";
        playButtonObj.SetActive(false);
        SetAllButtonsInteractable(true);
    }

    private void SetAllButtonsInteractable(bool interactable)
    {
        playButton.interactable = interactable;
        newGameButton.interactable = interactable;
        resetButton.interactable = interactable;
    }

    private void LoadRoom()
    {
        if (SceneTransitionManager.Inst != null)
            SceneTransitionManager.Inst.LoadScene("Room", "bed");
        else
            SceneManager.LoadScene("Room");
    }

    private void LoadOnboarding()
    {
        if (SceneTransitionManager.Inst != null)
            SceneTransitionManager.Inst.LoadScene("Onboarding");
        else
            SceneManager.LoadScene("Onboarding");
    }
}
