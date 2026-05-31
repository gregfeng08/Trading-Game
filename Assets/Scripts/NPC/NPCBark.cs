using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.API;
using Game.API.DTO;

public class NPCBark : MonoBehaviour
{
    private SpeechBubble bubble;
    private CanvasGroup hintGroup;
    private Transform hintRoot;
    private NPCWalker walker;
    private string[] customLines;
    private string assignedNpcType;
    private int lastIndex = -1;
    private Transform playerTransform;
    private bool hasBarkedThisVisit;
    private bool isPausedForBark;
    private float hintAlphaTarget;

    private const float HintRange = 5f;
    private const float BarkRange = 2.5f;
    private const float HintFadeSpeed = 3f;
    private static readonly Vector3 BubbleOffset = new(0f, 2.1f, 0f);
    private static readonly Vector3 HintOffset = new(0f, 2.0f, 0f);

    private static readonly Dictionary<string, List<string>> fetchedByType = new();
    private static readonly List<string> fetchedAll = new();
    private static string fetchedDate;
    private static bool fetchInProgress;

    private static readonly string[] FallbackMarketBarks =
    {
        "Markets are wild today...",
        "I should have sold yesterday.",
        "Buy low, sell high. Easier said than done.",
        "This volatility is killing me.",
        "My portfolio's looking rough lately...",
        "I heard big earnings are coming up.",
        "I need to rethink my strategy...",
        "Everyone's a genius in a bull market.",
        "Risk management. That's the whole game.",
        "Did you read the newspaper this morning?",
        "Don't put all your eggs in one basket.",
    };

    private static readonly string[] FallbackAmbientBarks =
    {
        "Nice weather we're having.",
        "Running late again...",
        "I could really go for a coffee.",
        "Excuse me, coming through.",
        "What a day...",
        "I wonder what's for lunch.",
        "This city never slows down.",
        "Have you tried that new place on 5th?",
        "I need a vacation.",
        "Traffic was terrible this morning.",
        "Almost forgot my umbrella again.",
        "Late nights at the office, you know how it is.",
        "Can't wait for the weekend.",
        "I swear this commute gets longer every day.",
    };

    public static async Task RefreshDialogue(string date)
    {
        if (date == fetchedDate || fetchInProgress) return;
        fetchInProgress = true;
        try
        {
            try
            {
                var entityId = APIBootstrapper.EntityExternalId;
                await DialogueAPI.Generate(entityId);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NPCBark] Dialogue generation skipped: {ex.Message}");
            }

            var resp = await DialogueAPI.GetDialogue(date);
            fetchedByType.Clear();
            fetchedAll.Clear();
            if (resp.dialogue != null)
            {
                foreach (var d in resp.dialogue)
                {
                    if (string.IsNullOrEmpty(d.text)) continue;
                    fetchedAll.Add(d.text);
                    if (!string.IsNullOrEmpty(d.npc_type))
                    {
                        if (!fetchedByType.ContainsKey(d.npc_type))
                            fetchedByType[d.npc_type] = new List<string>();
                        fetchedByType[d.npc_type].Add(d.text);
                    }
                }
            }
            fetchedDate = date;
            Debug.Log($"[NPCBark] Fetched {fetchedAll.Count} dialogue lines for {date} ({fetchedByType.Count} types)");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[NPCBark] Dialogue fetch failed, using fallback barks: {ex.Message}");
        }
        finally
        {
            fetchInProgress = false;
        }
    }

    public void Init(string[] barkLines = null, string npcType = null)
    {
        customLines = barkLines;
        assignedNpcType = npcType;
        walker = GetComponent<NPCWalker>();

        string currentDate = GamePhaseManager.Inst?.CurrentDate;
        if (!string.IsNullOrEmpty(currentDate) && currentDate != fetchedDate && !fetchInProgress)
            _ = RefreshDialogue(currentDate);

        var bubbleGO = new GameObject("SpeechBubble");
        bubbleGO.transform.SetParent(transform);
        bubble = bubbleGO.AddComponent<SpeechBubble>();
        bubble.Init(transform, BubbleOffset);

        BuildHint();
    }

    void Update()
    {
        if (playerTransform == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) playerTransform = player.transform;
            else return;
        }

        float dist = Vector3.Distance(transform.position, playerTransform.position);

        if (dist > HintRange)
        {
            hintAlphaTarget = 0f;
            if (bubble != null && bubble.IsActive) bubble.Hide();
            hasBarkedThisVisit = false;
        }
        else if (dist <= BarkRange)
        {
            hintAlphaTarget = 0f;
            if (!hasBarkedThisVisit && (bubble == null || !bubble.IsActive))
            {
                TriggerBark();
                hasBarkedThisVisit = true;
            }
            else if (bubble != null && bubble.IsActive)
            {
                bubble.SustainBark();
            }
        }
        else
        {
            hasBarkedThisVisit = false;
            if (bubble == null || !bubble.IsActive)
                hintAlphaTarget = 1f;
        }

        if (isPausedForBark && (bubble == null || !bubble.IsActive))
        {
            if (walker != null) walker.Resume();
            isPausedForBark = false;
        }

        if (hintGroup != null)
            hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, hintAlphaTarget, Time.deltaTime * HintFadeSpeed);
    }

    private void TriggerBark()
    {
        if (bubble == null) return;

        string line = PickRandomLine();
        if (line == null) return;

        bubble.ShowBark(line);

        if (walker != null)
        {
            walker.Pause();
            isPausedForBark = true;
        }
    }

    private string PickRandomLine()
    {
        if (customLines != null && customLines.Length > 0)
            return PickFrom(customLines);

        if (!string.IsNullOrEmpty(assignedNpcType)
            && fetchedByType.TryGetValue(assignedNpcType, out var typed)
            && typed.Count > 0)
            return PickFrom(typed);

        if (fetchedAll.Count > 0)
            return PickFrom(fetchedAll);

        bool pickMarket = Random.value < 0.4f;
        return pickMarket ? PickFrom(FallbackMarketBarks) : PickFrom(FallbackAmbientBarks);
    }

    private string PickFrom(IList<string> pool)
    {
        if (pool.Count == 0) return null;
        int index;
        do { index = Random.Range(0, pool.Count); }
        while (pool.Count > 1 && index == lastIndex);
        lastIndex = index;
        return pool[index];
    }

    private void BuildHint()
    {
        var hintGO = new GameObject("BarkHint");
        hintGO.transform.SetParent(transform);
        hintGO.transform.localPosition = HintOffset;
        hintRoot = hintGO.transform;

        var canvasGO = new GameObject("HintCanvas");
        canvasGO.transform.SetParent(hintGO.transform, false);
        canvasGO.transform.localPosition = Vector3.zero;
        canvasGO.transform.localScale = Vector3.one * 0.004f;

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 49;

        var rt = canvasGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(50f, 28f);
        rt.pivot = new Vector2(0.5f, 0f);

        hintGroup = canvasGO.AddComponent<CanvasGroup>();
        hintGroup.interactable = false;
        hintGroup.blocksRaycasts = false;
        hintGroup.alpha = 0f;

        var bg = new GameObject("BG");
        bg.transform.SetParent(canvasGO.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.1f, 0.75f);

        var textGO = new GameObject("Dots");
        textGO.transform.SetParent(bg.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "...";
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        tmp.raycastTarget = false;
    }
}
