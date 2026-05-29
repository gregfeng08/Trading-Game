using UnityEngine;

public class BedInteraction : MonoBehaviour
{
    private InteractionZone zone;

    void Awake()
    {
        zone = GetComponent<InteractionZone>();
    }

    void OnEnable()
    {
        if (GamePhaseManager.Inst != null)
            GamePhaseManager.Inst.OnPhaseChanged += OnPhaseChanged;
        UpdatePrompt();
    }

    void OnDisable()
    {
        if (GamePhaseManager.Inst != null)
            GamePhaseManager.Inst.OnPhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(GamePhase phase)
    {
        UpdatePrompt();
    }

    private void UpdatePrompt()
    {
        if (zone == null) return;

        var phase = GamePhaseManager.Inst != null
            ? GamePhaseManager.Inst.CurrentPhase
            : GamePhase.PostMarket;

        zone.SetInteractionName(phase switch
        {
            GamePhase.PreMarket => "Go to the Trading Terminal first",
            GamePhase.Day => "Take a Nap",
            _ => "Sleep"
        });
    }

    public void Interact()
    {
        if (GamePhaseManager.Inst == null) return;
        if (GamePhaseManager.Inst.CurrentPhase == GamePhase.PreMarket) return;

        switch (GamePhaseManager.Inst.CurrentPhase)
        {
            case GamePhase.Day:
                GamePhaseManager.Inst.EndDayEarly();
                break;

            case GamePhase.PostMarket:
                if (DailySummaryOverlay.Inst != null && !DailySummaryOverlay.Inst.IsActive)
                    DailySummaryOverlay.Inst.Show(() => _ = GamePhaseManager.Inst.AdvanceToNextDay());
                else
                    _ = GamePhaseManager.Inst.AdvanceToNextDay();
                break;
        }
    }
}
