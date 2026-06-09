using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using Game.API;
using Game.API.DTO;

public class NPCDialogueUI : MonoBehaviour
{
    public static NPCDialogueUI Inst { get; private set; }

    private GameObject overlayCanvas;
    private GameObject dialoguePanel;
    private TMP_Text nameText;
    private TMP_Text bodyText;
    private Button closeButton;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    public void Open(string npcType, string displayName)
    {
        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.OpenUI(PlayerState.TRADING, ClosePanel);

        nameText.text = displayName;
        bodyText.text = GetDialogueLine(npcType);

        dialoguePanel.SetActive(true);
        _ = RecordInteraction(npcType);
    }

    public void Close()
    {
        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.MOVING);
        else
            ClosePanel();
    }

    private void ClosePanel()
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    private string GetDialogueLine(string npcType)
    {
        var barks = NPCBark.GetFetchedLines(npcType);
        if (barks != null && barks.Count > 0)
            return barks[Random.Range(0, barks.Count)];
        return "...";
    }

    private async Task RecordInteraction(string npcType)
    {
        try
        {
            var req = new PlayerEventRequestDTO
            {
                entity_id = APIBootstrapper.EntityDbId,
                event_type = "npc_interaction",
                metadata_json = $"{{\"npc_type\":\"{npcType}\"}}"
            };
            await PlayerEventsAPI.RecordEvent(req);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[NPCDialogueUI] Failed to record interaction: {ex.Message}");
        }
    }

    private void BuildUI()
    {
        overlayCanvas = new GameObject("NPCDialogueCanvas");
        overlayCanvas.transform.SetParent(transform);
        var canvas = overlayCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 85;

        var scaler = overlayCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        overlayCanvas.AddComponent<GraphicRaycaster>();

        dialoguePanel = new GameObject("DialoguePanel");
        dialoguePanel.transform.SetParent(overlayCanvas.transform, false);
        var panelRect = dialoguePanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        // Dark background
        var bg = new GameObject("Background");
        bg.transform.SetParent(dialoguePanel.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.6f);
        var bgBtn = bg.AddComponent<Button>();
        bgBtn.transition = Selectable.Transition.None;
        bgBtn.onClick.AddListener(Close);

        // Dialogue box (bottom-center)
        var box = new GameObject("DialogueBox");
        box.transform.SetParent(dialoguePanel.transform, false);
        var boxRect = box.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.15f, 0.05f);
        boxRect.anchorMax = new Vector2(0.85f, 0.35f);
        boxRect.sizeDelta = Vector2.zero;
        var boxImg = box.AddComponent<Image>();
        boxImg.color = new Color(0.1f, 0.1f, 0.14f, 0.95f);

        // NPC name
        var nameGO = new GameObject("Name");
        nameGO.transform.SetParent(box.transform, false);
        var nameRect = nameGO.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 1);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0, -10);
        nameRect.sizeDelta = new Vector2(-40, 36);
        nameText = nameGO.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 22;
        nameText.fontStyle = FontStyles.Bold;
        nameText.color = new Color(0.4f, 0.75f, 1f);
        nameText.alignment = TextAlignmentOptions.TopLeft;

        // Body text
        var bodyGO = new GameObject("Body");
        bodyGO.transform.SetParent(box.transform, false);
        var bodyRect = bodyGO.AddComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0, 0.15f);
        bodyRect.anchorMax = new Vector2(1, 0.85f);
        bodyRect.sizeDelta = Vector2.zero;
        bodyRect.offsetMin = new Vector2(20, 0);
        bodyRect.offsetMax = new Vector2(-20, 0);
        bodyText = bodyGO.AddComponent<TextMeshProUGUI>();
        bodyText.fontSize = 18;
        bodyText.color = new Color(0.85f, 0.85f, 0.9f);
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.enableWordWrapping = true;

        // Close button
        var closeBtnGO = new GameObject("CloseButton");
        closeBtnGO.transform.SetParent(box.transform, false);
        var closeBtnRect = closeBtnGO.AddComponent<RectTransform>();
        closeBtnRect.anchorMin = new Vector2(1, 1);
        closeBtnRect.anchorMax = new Vector2(1, 1);
        closeBtnRect.pivot = new Vector2(1, 1);
        closeBtnRect.anchoredPosition = new Vector2(-5, -5);
        closeBtnRect.sizeDelta = new Vector2(30, 30);
        var closeBtnImg = closeBtnGO.AddComponent<Image>();
        closeBtnImg.color = new Color(0.3f, 0.15f, 0.15f, 0.8f);
        closeButton = closeBtnGO.AddComponent<Button>();
        closeButton.onClick.AddListener(Close);

        var xGO = new GameObject("X");
        xGO.transform.SetParent(closeBtnGO.transform, false);
        var xRect = xGO.AddComponent<RectTransform>();
        xRect.anchorMin = Vector2.zero;
        xRect.anchorMax = Vector2.one;
        xRect.sizeDelta = Vector2.zero;
        var xText = xGO.AddComponent<TextMeshProUGUI>();
        xText.text = "X";
        xText.fontSize = 16;
        xText.alignment = TextAlignmentOptions.Center;
        xText.color = new Color(0.9f, 0.6f, 0.6f);

        dialoguePanel.SetActive(false);
    }
}
