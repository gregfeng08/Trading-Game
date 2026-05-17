using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;
using Game.API;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject continueButtonObj;
    [SerializeField] private GameObject newGameButtonObj;

    [Header("Bootstrap")]
    [SerializeField] private APIBootstrapper bootstrapper;

    private Coroutine _poll;
    private Button continueButton;
    private Button newGameButton;

    private void Awake()
    {
        continueButton = continueButtonObj.GetComponent<Button>();
        newGameButton = newGameButtonObj.GetComponent<Button>();
    }

    private void OnEnable()
    {
        continueButtonObj.SetActive(false);
        newGameButtonObj.SetActive(false);

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
            continueButtonObj.SetActive(true);
            continueButton.onClick.AddListener(OnContinue);
        }

        newGameButtonObj.SetActive(true);
        newGameButton.onClick.AddListener(OnNewGame);

        statusText.text = hasSave ? "Welcome back" : "Ready to start";
    }

    private void OnContinue()
    {
        continueButton.interactable = false;
        newGameButton.interactable = false;
        LoadRoom();
    }

    private void OnNewGame()
    {
        continueButton.interactable = false;
        newGameButton.interactable = false;
        StartCoroutine(NewGameFlow());
    }

    private IEnumerator NewGameFlow()
    {
        statusText.text = "Creating new game...";

        var task = GameStateAPI.NewGame("2020-01-01");
        yield return new WaitUntil(() => task.IsCompleted);

        if (!task.IsCompletedSuccessfully)
        {
            statusText.text = "Failed to create game. Try again.";
            continueButton.interactable = true;
            newGameButton.interactable = true;
            yield break;
        }

        // Re-initialize knowledge graph for fresh start
        if (KnowledgeGraphManager.Inst != null)
        {
            var kgTask = KnowledgeGraphManager.Inst.InitializeAsync();
            yield return new WaitUntil(() => kgTask.IsCompleted);
        }

        LoadRoom();
    }

    private void LoadRoom()
    {
        if (SceneTransitionManager.Inst != null)
            SceneTransitionManager.Inst.LoadScene("Room", "bed");
        else
            SceneManager.LoadScene("Room");
    }
}
