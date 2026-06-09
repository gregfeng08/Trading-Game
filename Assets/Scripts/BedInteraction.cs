using UnityEngine;

public class BedInteraction : MonoBehaviour
{
    private InteractionZone zone;
    private MeshRenderer meshRenderer;
    private Collider col;
    private bool subscribedToPhase;
    private bool awaitingConfirm;
    private float confirmTimer;
    private string originalInteractionName;

    void Awake()
    {
        zone = GetComponent<InteractionZone>();
        meshRenderer = GetComponent<MeshRenderer>();
        col = GetComponent<Collider>();
    }

    void OnEnable()
    {
        TrySubscribe();
        UpdateVisibility();
    }

    void OnDisable()
    {
        if (GamePhaseManager.Inst != null)
            GamePhaseManager.Inst.OnPhaseChanged -= OnPhaseChanged;
        subscribedToPhase = false;
    }

    void Update()
    {
        if (!subscribedToPhase)
            TrySubscribe();

        if (awaitingConfirm)
        {
            confirmTimer -= Time.deltaTime;
            if (confirmTimer <= 0f)
            {
                awaitingConfirm = false;
                if (zone != null && originalInteractionName != null)
                    zone.SetInteractionName(originalInteractionName);
            }
        }
    }

    private void TrySubscribe()
    {
        if (subscribedToPhase || GamePhaseManager.Inst == null) return;
        GamePhaseManager.Inst.OnPhaseChanged += OnPhaseChanged;
        subscribedToPhase = true;
    }

    private void OnPhaseChanged(GamePhase phase)
    {
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        var phase = GamePhaseManager.Inst != null
            ? GamePhaseManager.Inst.CurrentPhase
            : GamePhase.PostMarket;

        if (phase == GamePhase.PreMarket)
        {
            if (meshRenderer != null) meshRenderer.enabled = false;
            if (col != null) col.enabled = false;
            if (InteractionsController.Inst != null && zone != null)
                InteractionsController.Inst.ClearActiveZone(zone);
            return;
        }

        if (meshRenderer != null) meshRenderer.enabled = true;
        if (col != null) col.enabled = true;
        if (zone != null)
            zone.SetInteractionName(phase == GamePhase.Day ? "Take a Nap" : "Sleep");
    }

    public void Interact()
    {
        if (GamePhaseManager.Inst == null) return;
        if (GamePhaseManager.Inst.CurrentPhase == GamePhase.PreMarket) return;

        if (GameSettings.RequireDoubleConfirm && !awaitingConfirm)
        {
            awaitingConfirm = true;
            confirmTimer = 3f;
            if (zone != null)
            {
                originalInteractionName = GamePhaseManager.Inst.CurrentPhase == GamePhase.Day
                    ? "Take a Nap" : "Sleep";
                zone.SetInteractionName("Are you sure? (press E again)");
            }
            return;
        }
        awaitingConfirm = false;

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
