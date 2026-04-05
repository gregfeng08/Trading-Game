using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;
using Game.API;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject playButtonObj;

    [Header("Bootstrap")]
    [SerializeField] private APIBootstrapper bootstrapper;

    private Coroutine _poll;
    private Button playButton;
    private TMP_Text playButtonText;

    private void Awake()
    {
        playButton = playButtonObj.GetComponent<Button>();
        playButtonText = playButtonObj.GetComponentInChildren<TMP_Text>(true);
    }

    private void OnEnable()
    {
        playButtonText.text = "Waiting...";
        playButton.interactable = false;

        bootstrapper.TryStart(); // instance call

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
                    // if NotStarted for some reason, kick it
                    bootstrapper.TryStart();
                    break;

                case APIBootstrapper.BootState.Bootstrapping:
                    statusText.text = "Connecting...";
                    break;

                case APIBootstrapper.BootState.Ready:
                    statusText.text = "Server Ready";
                    playButton.interactable = true;
                    playButton.onClick.AddListener(() =>
                    {
                        SceneManager.LoadScene("Room");
                    });
                    playButtonText.text = "Play";
                    yield break;

                case APIBootstrapper.BootState.Failed:
                    statusText.text = "Failed to connect. Retrying...";
                    // Reset and retry after a delay
                    APIBootstrapper.ResetForRetry();
                    yield return new WaitForSeconds(1.0f);
                    bootstrapper.TryStart();
                    break;
            }

            yield return new WaitForSeconds(0.5f);
        }
    }
}