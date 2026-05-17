using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class KnowledgeGraphInteraction : MonoBehaviour
{
    [Header("Visual Indicator")]
    [SerializeField] private GameObject badgeIndicator;
    [SerializeField] private TMP_Text badgeCountText;

    void Start()
    {
        UpdateBadge(0);
    }

    void OnEnable()
    {
        if (KnowledgeGraphManager.Inst != null)
            KnowledgeGraphManager.Inst.OnPendingCountChanged += UpdateBadge;
    }

    void OnDisable()
    {
        if (KnowledgeGraphManager.Inst != null)
            KnowledgeGraphManager.Inst.OnPendingCountChanged -= UpdateBadge;
    }

    public void OpenGraph()
    {
        if (KnowledgeGraphUI.Inst != null)
            KnowledgeGraphUI.Inst.Open();
    }

    private void UpdateBadge(int count)
    {
        if (badgeIndicator != null)
            badgeIndicator.SetActive(count > 0);
        if (badgeCountText != null)
            badgeCountText.text = count.ToString();
    }
}
