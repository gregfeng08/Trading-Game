using UnityEngine;

public class BedInteraction : MonoBehaviour
{
    public void Interact()
    {
        if (GamePhaseManager.Inst == null) return;

        switch (GamePhaseManager.Inst.CurrentPhase)
        {
            case GamePhase.Day:
                GamePhaseManager.Inst.EndDayEarly();
                break;

            case GamePhase.PostMarket:
                _ = GamePhaseManager.Inst.AdvanceToNextDay();
                break;
        }
    }
}
