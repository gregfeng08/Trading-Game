using UnityEngine;
using TMPro;

public class DayCycleHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text phaseText;
    [SerializeField] private TMP_Text arcText;

    void Update()
    {
        if (GamePhaseManager.Inst == null) return;

        var gpm = GamePhaseManager.Inst;
        string date = gpm.CurrentDate ?? "---";

        if (phaseText != null)
        {
            phaseText.text = gpm.CurrentPhase switch
            {
                GamePhase.PreMarket => $"{date} | PRE-MARKET",
                GamePhase.Day => $"{date} | DAY ({FormatTime(gpm.DayTimeRemaining)})",
                GamePhase.PostMarket => $"{date} | POST-MARKET",
                _ => date
            };
        }

        if (arcText != null)
        {
            if (gpm.ArcName != null && gpm.ArcDaysRemaining >= 0)
            {
                string grade = gpm.ArcProjectedGrade ?? "-";
                string ret = gpm.ArcReturnPct >= 0 ? $"+{gpm.ArcReturnPct:F1}%" : $"{gpm.ArcReturnPct:F1}%";
                arcText.text = $"{gpm.ArcName}  |  {gpm.ArcDaysRemaining}d left  |  {ret} ({grade})";
            }
            else
            {
                arcText.text = "";
            }
        }
    }

    private string FormatTime(float seconds)
    {
        int min = Mathf.FloorToInt(seconds / 60f);
        int sec = Mathf.FloorToInt(seconds % 60f);
        return $"{min}:{sec:D2}";
    }
}
