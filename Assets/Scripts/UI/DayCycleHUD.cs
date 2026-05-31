using UnityEngine;
using TMPro;

public class DayCycleHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text phaseText;
    [SerializeField] private TMP_Text arcText;
    private TMP_Text cashHudText;

    [Header("Layout")]
    [SerializeField] private float leftMargin = 15f;
    [SerializeField] private float topMargin = 15f;
    [SerializeField] private float lineSpacing = 25f;

    void Start()
    {
        CreateCashText();
        AnchorToTopLeft();
    }

    private void CreateCashText()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("CashHudText");
        go.transform.SetParent(canvas.transform, false);
        cashHudText = go.AddComponent<TextMeshProUGUI>();
        cashHudText.fontSize = phaseText != null ? phaseText.fontSize : 16;
        cashHudText.color = phaseText != null ? phaseText.color : Color.white;
        cashHudText.alignment = TextAlignmentOptions.TopLeft;
        cashHudText.enableAutoSizing = false;
        cashHudText.raycastTarget = false;
    }

    private void AnchorToTopLeft()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var canvasRect = canvas.GetComponent<RectTransform>();

        if (phaseText != null)
        {
            phaseText.rectTransform.SetParent(canvasRect, false);
            phaseText.rectTransform.anchorMin = new Vector2(0f, 1f);
            phaseText.rectTransform.anchorMax = new Vector2(0f, 1f);
            phaseText.rectTransform.pivot = new Vector2(0f, 1f);
            phaseText.rectTransform.anchoredPosition = new Vector2(leftMargin, -topMargin);
            phaseText.rectTransform.sizeDelta = new Vector2(500f, 30f);
            phaseText.alignment = TextAlignmentOptions.TopLeft;
            phaseText.enableAutoSizing = false;
        }

        if (arcText != null)
        {
            arcText.rectTransform.SetParent(canvasRect, false);
            arcText.rectTransform.anchorMin = new Vector2(0f, 1f);
            arcText.rectTransform.anchorMax = new Vector2(0f, 1f);
            arcText.rectTransform.pivot = new Vector2(0f, 1f);
            arcText.rectTransform.anchoredPosition = new Vector2(leftMargin, -topMargin - lineSpacing);
            arcText.rectTransform.sizeDelta = new Vector2(500f, 30f);
            arcText.alignment = TextAlignmentOptions.TopLeft;
            arcText.enableAutoSizing = false;
        }

        if (cashHudText != null)
        {
            cashHudText.rectTransform.SetParent(canvasRect, false);
            cashHudText.rectTransform.anchorMin = new Vector2(0f, 1f);
            cashHudText.rectTransform.anchorMax = new Vector2(0f, 1f);
            cashHudText.rectTransform.pivot = new Vector2(0f, 1f);
            cashHudText.rectTransform.anchoredPosition = new Vector2(leftMargin, -topMargin - lineSpacing * 2);
            cashHudText.rectTransform.sizeDelta = new Vector2(500f, 30f);
            cashHudText.alignment = TextAlignmentOptions.TopLeft;
            cashHudText.enableAutoSizing = false;
        }
    }

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
                if (ProgressionGates.ShowExtendedArcInfo)
                {
                    string grade = gpm.ArcProjectedGrade ?? "-";
                    string ret = gpm.ArcReturnPct >= 0 ? $"+{gpm.ArcReturnPct:F1}%" : $"{gpm.ArcReturnPct:F1}%";
                    arcText.text = $"{gpm.ArcName}  |  {gpm.ArcDaysRemaining}d left  |  {ret} ({grade})";
                }
                else
                {
                    arcText.text = gpm.ArcName;
                }
            }
            else
            {
                arcText.text = "";
            }
        }

        if (cashHudText != null)
        {
            double cash = gpm.EffectiveAvailableCash;
            string text = $"Cash: ${cash:N2}";
            if (ProgressionGates.ShowNetWorth && gpm.ServerNetWorth > 0)
                text += $"  |  Net Worth: ${gpm.ServerNetWorth:N2}";
            cashHudText.text = text;
        }
    }

    private string FormatTime(float seconds)
    {
        int min = Mathf.FloorToInt(seconds / 60f);
        int sec = Mathf.FloorToInt(seconds % 60f);
        return $"{min}:{sec:D2}";
    }
}
