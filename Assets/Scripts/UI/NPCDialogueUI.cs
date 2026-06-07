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
    private TMP_Text questText;
    private Button closeButton;
    private Button questButton;
    private TMP_Text questButtonLabel;

    private string currentNpcType;
    private NpcQuestDTO[] cachedQuests;
    private bool questsLoaded;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        _ = LoadQuests();
    }

    private async Task LoadQuests()
    {
        try
        {
            var resp = await PlayerEventsAPI.GetQuests();
            cachedQuests = resp.quests;
            questsLoaded = true;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[NPCDialogueUI] Failed to load quests: {ex.Message}");
        }
    }

    public void Open(string npcType, string displayName)
    {
        currentNpcType = npcType;

        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.OpenUI(PlayerState.TRADING, ClosePanel);

        nameText.text = displayName;
        bodyText.text = GetDialogueLine(npcType);

        var quest = FindQuest(npcType);
        bool hasQuest = quest != null;
        questText.gameObject.SetActive(hasQuest);
        questButton.gameObject.SetActive(hasQuest);
        if (hasQuest)
        {
            questText.text = quest.quest_dialogue_hint ?? $"{displayName} can teach you something new.";
            questButtonLabel.text = "Learn";
        }

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

    private NpcQuestDTO FindQuest(string npcType)
    {
        if (!questsLoaded || cachedQuests == null) return null;
        foreach (var q in cachedQuests)
            if (q.npc_type == npcType) return q;
        return null;
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

    private async void OnQuestComplete()
    {
        if (string.IsNullOrEmpty(currentNpcType)) return;

        questButton.interactable = false;
        questButtonLabel.text = "Learning...";

        try
        {
            var req = new NpcQuestCompleteRequestDTO
            {
                entity_id = APIBootstrapper.EntityDbId,
                npc_type = currentNpcType
            };
            var resp = await PlayerEventsAPI.CompleteQuest(req);

            if (resp.unlocked_nodes != null && resp.unlocked_nodes.Length > 0)
            {
                questText.text = $"<color=#26BF59>Unlocked {resp.unlocked_nodes.Length} new concept(s)!</color>";
                questButton.gameObject.SetActive(false);

                if (KnowledgeGraphManager.Inst != null)
                    _ = KnowledgeGraphManager.Inst.CheckTriggersAsync();
            }
            else
            {
                questText.text = "Nothing new to learn right now.";
                questButton.gameObject.SetActive(false);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[NPCDialogueUI] Quest completion failed: {ex.Message}");
            questButtonLabel.text = "Learn";
            questButton.interactable = true;
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
        bodyRect.anchorMin = new Vector2(0, 0.3f);
        bodyRect.anchorMax = new Vector2(1, 0.85f);
        bodyRect.sizeDelta = Vector2.zero;
        bodyRect.offsetMin = new Vector2(20, 0);
        bodyRect.offsetMax = new Vector2(-20, 0);
        bodyText = bodyGO.AddComponent<TextMeshProUGUI>();
        bodyText.fontSize = 18;
        bodyText.color = new Color(0.85f, 0.85f, 0.9f);
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.enableWordWrapping = true;

        // Quest hint
        var questGO = new GameObject("QuestHint");
        questGO.transform.SetParent(box.transform, false);
        var questRect = questGO.AddComponent<RectTransform>();
        questRect.anchorMin = new Vector2(0, 0);
        questRect.anchorMax = new Vector2(0.7f, 0.3f);
        questRect.sizeDelta = Vector2.zero;
        questRect.offsetMin = new Vector2(20, 10);
        questRect.offsetMax = new Vector2(-10, -5);
        questText = questGO.AddComponent<TextMeshProUGUI>();
        questText.fontSize = 15;
        questText.fontStyle = FontStyles.Italic;
        questText.color = new Color(0.7f, 0.8f, 0.6f);
        questText.alignment = TextAlignmentOptions.BottomLeft;
        questText.enableWordWrapping = true;

        // Quest button
        var questBtnGO = new GameObject("QuestButton");
        questBtnGO.transform.SetParent(box.transform, false);
        var questBtnRect = questBtnGO.AddComponent<RectTransform>();
        questBtnRect.anchorMin = new Vector2(0.75f, 0.05f);
        questBtnRect.anchorMax = new Vector2(0.95f, 0.25f);
        questBtnRect.sizeDelta = Vector2.zero;
        var questBtnImg = questBtnGO.AddComponent<Image>();
        questBtnImg.color = new Color(0.2f, 0.5f, 0.3f, 1f);
        questButton = questBtnGO.AddComponent<Button>();
        questButton.onClick.AddListener(OnQuestComplete);

        var qLabelGO = new GameObject("Label");
        qLabelGO.transform.SetParent(questBtnGO.transform, false);
        var qLabelRect = qLabelGO.AddComponent<RectTransform>();
        qLabelRect.anchorMin = Vector2.zero;
        qLabelRect.anchorMax = Vector2.one;
        qLabelRect.sizeDelta = Vector2.zero;
        questButtonLabel = qLabelGO.AddComponent<TextMeshProUGUI>();
        questButtonLabel.text = "Learn";
        questButtonLabel.fontSize = 16;
        questButtonLabel.alignment = TextAlignmentOptions.Center;
        questButtonLabel.color = Color.white;

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
