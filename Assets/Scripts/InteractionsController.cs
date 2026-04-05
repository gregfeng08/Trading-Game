using UnityEngine;
using TMPro;

public enum InteractionType
{
    Movement,   // Scene transitions
    GameState,  // End day, save, etc.
    UI          // Trading terminal, pause menu, etc.
}

/// <summary>
/// Singleton that centralizes all player interactions.
/// Owns the shared prompt UI and handles E-key input.
/// InteractionZones register/unregister as the player enters/exits triggers.
/// </summary>
public class InteractionsController : MonoBehaviour
{
    public static InteractionsController Inst { get; private set; }

    [Header("Prompt UI")]
    [SerializeField] private GameObject promptPanel;
    [SerializeField] private TMP_Text promptText;

    private InteractionZone activeZone;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;

        if (promptPanel != null)
            promptPanel.SetActive(false);
    }

    void Update()
    {
        if (activeZone == null) return;

        if (PlayerStateController.Inst != null && !PlayerStateController.Inst.CanInteract)
            return;

        if (Input.GetKeyDown(KeyCode.E))
            Execute(activeZone);
    }

    public void SetActiveZone(InteractionZone zone)
    {
        activeZone = zone;

        if (promptPanel != null)
        {
            promptPanel.SetActive(true);
            if (promptText != null)
                promptText.text = $"Press E to {zone.InteractionName}";
        }
    }

    public void ClearActiveZone(InteractionZone zone)
    {
        if (activeZone != zone) return;
        activeZone = null;

        if (promptPanel != null)
            promptPanel.SetActive(false);
    }

    private void Execute(InteractionZone zone)
    {
        zone.Execute();
    }
}
