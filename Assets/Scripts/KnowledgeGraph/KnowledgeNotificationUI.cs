using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.API.DTO;

public class KnowledgeNotificationUI : MonoBehaviour
{
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button learnMoreButton;
    [SerializeField] private Button dismissButton;
    [SerializeField] private float autoDismissTime = 8f;

    private UnlockedNodeDTO currentNode;
    private float showTimer;

    void Start()
    {
        if (notificationPanel != null)
            notificationPanel.SetActive(false);

        if (learnMoreButton != null)
            learnMoreButton.onClick.AddListener(OnLearnMore);
        if (dismissButton != null)
            dismissButton.onClick.AddListener(Dismiss);
    }

    void OnEnable()
    {
        if (KnowledgeGraphManager.Inst != null)
            KnowledgeGraphManager.Inst.OnHighPriorityUnlock += ShowNotification;
    }

    void OnDisable()
    {
        if (KnowledgeGraphManager.Inst != null)
            KnowledgeGraphManager.Inst.OnHighPriorityUnlock -= ShowNotification;
    }

    void Update()
    {
        if (notificationPanel == null || !notificationPanel.activeSelf) return;

        showTimer -= Time.deltaTime;
        if (showTimer <= 0f)
            Dismiss();
    }

    private void ShowNotification(UnlockedNodeDTO node)
    {
        if (PlayerStateController.Inst != null &&
            PlayerStateController.Inst.State == PlayerState.TRADING)
        {
            return;
        }

        currentNode = node;
        showTimer = autoDismissTime;

        if (titleText != null)
            titleText.text = $"New Insight: {node.title}";
        if (descriptionText != null)
            descriptionText.text = "Press 'Learn More' to explore this concept.";

        notificationPanel.SetActive(true);
    }

    private void OnLearnMore()
    {
        Dismiss();
        if (KnowledgeGraphUI.Inst != null)
            KnowledgeGraphUI.Inst.OpenToNode(currentNode?.node_id);
    }

    private void Dismiss()
    {
        if (notificationPanel != null)
            notificationPanel.SetActive(false);
        currentNode = null;
    }
}
