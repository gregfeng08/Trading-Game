using UnityEngine;
using TMPro;

public class DayCycleHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text phaseText;

    void Update()
    {
        if (GamePhaseManager.Inst == null || phaseText == null) return;

        var gpm = GamePhaseManager.Inst;
        string date = gpm.CurrentDate ?? "---";

        phaseText.text = gpm.CurrentPhase switch
        {
            GamePhase.PreMarket => $"{date} | PRE-MARKET",
            GamePhase.Day => $"{date} | DAY ({FormatTime(gpm.DayTimeRemaining)})",
            GamePhase.PostMarket => $"{date} | POST-MARKET",
            _ => date
        };
    }

    private string FormatTime(float seconds)
    {
        int min = Mathf.FloorToInt(seconds / 60f);
        int sec = Mathf.FloorToInt(seconds % 60f);
        return $"{min}:{sec:D2}";
    }
}
