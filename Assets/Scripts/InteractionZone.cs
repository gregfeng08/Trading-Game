using UnityEngine;
using UnityEngine.Events;

public class InteractionZone : MonoBehaviour
{
    [SerializeField] private InteractionType interactionType;
    [SerializeField] private string interactionName;
    [SerializeField] private UnityEvent onInteract;

    public InteractionType Type => interactionType;
    public string InteractionName => interactionName;

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
}
