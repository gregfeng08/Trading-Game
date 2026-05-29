using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class InteractionZone : MonoBehaviour
{
    [SerializeField] private InteractionType interactionType;
    [SerializeField] private string interactionName;
    [SerializeField] private string spawnPointName;
    [SerializeField] private UnityEvent onInteract;

    public InteractionType Type => interactionType;
    public string InteractionName => overrideName ?? interactionName;

    private string overrideName;

    public void Configure(InteractionType type, string name, UnityEngine.Events.UnityAction action)
    {
        interactionType = type;
        interactionName = name;
        if (action != null)
            onInteract.AddListener(action);
    }

    public void SetInteractionName(string name)
    {
        overrideName = name;
        if (InteractionsController.Inst != null)
            InteractionsController.Inst.RefreshPrompt(this);
    }

    public void Execute()
    {
        onInteract.Invoke();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && InteractionsController.Inst != null)
            InteractionsController.Inst.SetActiveZone(this);
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && InteractionsController.Inst != null)
            InteractionsController.Inst.ClearActiveZone(this);
    }

    public void LoadScene(string sceneName)
    {
        if (SceneTransitionManager.Inst != null)
            SceneTransitionManager.Inst.LoadScene(sceneName, spawnPointName);
        else
            SceneManager.LoadScene(sceneName);
    }
}
