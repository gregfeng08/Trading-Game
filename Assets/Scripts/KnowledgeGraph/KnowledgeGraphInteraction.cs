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
        if (KnowledgeGraphManager.Inst != null)
            UpdateBadge(KnowledgeGraphManager.Inst.PendingUnlockedCount);
        else
            UpdateBadge(0);
    }

    void OnEnable()
    {
        if (KnowledgeGraphManager.Inst != null)
        {
            KnowledgeGraphManager.Inst.OnPendingCountChanged += UpdateBadge;
            KnowledgeGraphManager.Inst.OnInitialized += OnGraphInitialized;
            KnowledgeGraphManager.Inst.OnGraphRefreshed += OnGraphRefreshed;
            UpdateBadge(KnowledgeGraphManager.Inst.PendingUnlockedCount);
        }
    }

    void OnDisable()
    {
        if (KnowledgeGraphManager.Inst != null)
        {
            KnowledgeGraphManager.Inst.OnPendingCountChanged -= UpdateBadge;
            KnowledgeGraphManager.Inst.OnInitialized -= OnGraphInitialized;
            KnowledgeGraphManager.Inst.OnGraphRefreshed -= OnGraphRefreshed;
        }
    }

    private void OnGraphInitialized()
    {
        UpdateBadge(KnowledgeGraphManager.Inst.PendingUnlockedCount);
    }

    private void OnGraphRefreshed()
    {
        UpdateBadge(KnowledgeGraphManager.Inst.PendingUnlockedCount);
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
