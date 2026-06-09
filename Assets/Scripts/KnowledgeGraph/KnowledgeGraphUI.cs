using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Game.API;
using Game.API.DTO;

public class KnowledgeGraphUI : MonoBehaviour
{
    public static KnowledgeGraphUI Inst { get; private set; }

    [Header("Panel")]
    [SerializeField] private GameObject graphPanel;
    [SerializeField] private Button closeButton;

    [Header("Graph Container")]
    [SerializeField] private RectTransform graphContainer;
    [SerializeField] private GameObject nodePrefab;
    [SerializeField] private float graphScale = 1.5f;
    [SerializeField] private float zoomSpeed = 0.1f;
    [SerializeField] private float minZoom = 0.5f;
    [SerializeField] private float maxZoom = 2.5f;

    [Header("Detail Panel")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private TMP_Text detailTitle;
    [SerializeField] private TMP_Text detailContent;
    [SerializeField] private TMP_Text detailStatus;
    [SerializeField] private Button completeButton;

    [Header("Knowledge Node Colors")]
    [SerializeField] private Color lockedColor = new Color(0.22f, 0.22f, 0.25f, 1f);
    [SerializeField] private Color unlockedColor = new Color(0.9f, 0.7f, 0.1f, 1f);
    [SerializeField] private Color completedColor = new Color(0.2f, 0.8f, 0.3f, 1f);

    [Header("Adaptive Node Colors")]
    [SerializeField] private Color adaptiveLockedColor = new Color(0.2f, 0.2f, 0.28f, 1f);
    [SerializeField] private Color adaptiveReadyColor = new Color(0.25f, 0.4f, 0.65f, 1f);
    [SerializeField] private Color adaptiveUnlockedColor = new Color(0.9f, 0.5f, 0.1f, 1f);
    [SerializeField] private Color adaptiveCompletedColor = new Color(0.2f, 0.8f, 0.3f, 1f);

    [Header("Edges")]
    [SerializeField] private Color edgeColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Header("Auto Layout")]
    [SerializeField] private float tierSpacing = 300f;
    [SerializeField] private float nodeSpacing = 140f;
    [SerializeField] private int forceIterations = 30;
    [SerializeField] private float repulsionStrength = 800f;
    [SerializeField] private float attractionStrength = 0.15f;

    [Header("Focus")]
    [SerializeField] private Color focusBorderColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private float focusBorderWidth = 3f;
    [SerializeField] private float focusPanDuration = 0.3f;

    private List<GameObject> spawnedNodes = new();
    private List<GameObject> spawnedEdges = new();
    private KnowledgeNodeStateDTO selectedNode;
    private Dictionary<string, RectTransform> nodePositions = new();
    private Dictionary<string, KnowledgeNodeStateDTO> nodeLookup = new();
    private Dictionary<string, Image> nodeImages = new();
    private Dictionary<string, HashSet<string>> neighbors = new();
    private List<(string fromId, string toId, GameObject edgeGO)> edgeRegistry = new();

    // Unified node color state
    private string highlightedNodeId;
    private string hoveredNodeId;

    private bool isDragging;
    private Vector2 lastMousePos;
    private Vector2 panOffset;
    private float currentZoom = 1f;

    // Arrow key navigation
    private string focusedNodeId;
    private GameObject focusOutline;
    private Vector2 panTarget;
    private bool isPanAnimating;

    // Casey dialogue box (screen-space, bottom of graph panel)
    private GameObject caseyDialogueBox;
    private TMP_Text caseyNameText;
    private TMP_Text caseyBodyText;
    private TMP_Text caseyPromptText;
    private Button caseyTakeButton;
    private string[] caseyLines;
    private int caseyLineIndex;
    private bool caseyIsTyping;
    private int caseyTotalChars;
    private float caseyCharAccum;
    private const float CaseyCharsPerSec = 45f;
    // Completion overlay (centered, shown on Complete/Casey's Take)
    private GameObject completionOverlay;
    private CanvasGroup completionOverlayGroup;
    private RectTransform completionCardRT;
    private TMP_Text completionCategoryText;
    private TMP_Text completionTitleText;
    private ScrollRect completionScrollRect;
    private TMP_Text completionBodyText;
    private Button completionGotItButton;
    private string pendingCompletionNodeId;
    private bool completionOverlayActive;

    private CanvasGroup graphCanvasGroup;
    private float graphFadeTarget;
    private const float GraphFadeSpeed = 4f;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
    }

    void Start()
    {
        if (graphPanel != null)
            graphPanel.SetActive(false);
        if (detailPanel != null)
            detailPanel.SetActive(false);
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        if (completeButton != null)
            completeButton.onClick.AddListener(OnCompleteLesson);
    }

    void Update()
    {
        if (graphPanel == null || !graphPanel.activeSelf) return;

        if (graphCanvasGroup != null && graphCanvasGroup.alpha < graphFadeTarget)
        {
            graphCanvasGroup.alpha = Mathf.MoveTowards(graphCanvasGroup.alpha, graphFadeTarget, GraphFadeSpeed * Time.deltaTime);
            if (graphCanvasGroup.alpha >= 0.99f)
                graphCanvasGroup.alpha = 1f;
        }

        UpdateCaseyTypewriter();

        if (caseyDialogueBox != null && caseyDialogueBox.activeSelf)
        {
            HandleCaseyInput();
            return;
        }

        if (completionOverlayActive)
        {
            HandleCompletionOverlayInput();
            return;
        }

        HandlePan();
        HandleZoom();
        HandleKeyboardNav();
        AnimatePan();
    }

    private void HandlePan()
    {
        if (Input.GetMouseButtonDown(0) && !IsPointerOverNode())
        {
            isDragging = true;
            isPanAnimating = false;
            lastMousePos = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0))
            isDragging = false;

        if (isDragging)
        {
            Vector2 currentMousePos = Input.mousePosition;
            Vector2 delta = (currentMousePos - lastMousePos) / currentZoom;
            lastMousePos = currentMousePos;

            panOffset += delta;
            graphContainer.anchoredPosition = panOffset;
        }
    }

    private void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scroll, 0f)) return;

        float prevZoom = currentZoom;
        currentZoom = Mathf.Clamp(currentZoom + scroll * zoomSpeed, minZoom, maxZoom);

        graphContainer.localScale = Vector3.one * currentZoom;

        // Zoom toward mouse position
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            graphContainer.parent as RectTransform, Input.mousePosition, null, out var localMouse);
        float scaleFactor = currentZoom / prevZoom;
        panOffset = localMouse - (localMouse - panOffset) * scaleFactor;
        graphContainer.anchoredPosition = panOffset;
    }

    private void HandleKeyboardNav()
    {
        if (nodePositions.Count == 0) return;

        Vector2 dir = Vector2.zero;
        if (Input.GetKeyDown(KeyCode.RightArrow)) dir = Vector2.right;
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) dir = Vector2.left;
        else if (Input.GetKeyDown(KeyCode.UpArrow)) dir = Vector2.up;
        else if (Input.GetKeyDown(KeyCode.DownArrow)) dir = Vector2.down;
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            // If Casey dialogue is active, advance it
            if (caseyDialogueBox != null && caseyDialogueBox.activeSelf)
            {
                HandleCaseyInput();
                return;
            }

            // If detail panel is open, activate the visible button
            if (selectedNode != null)
            {
                if (caseyTakeButton != null && caseyTakeButton.gameObject.activeSelf)
                    OnCaseyTakeClicked();
                else if (completeButton != null && completeButton.gameObject.activeSelf)
                    OnCompleteLesson();
                return;
            }

            // Otherwise, open detail for focused node
            if (focusedNodeId != null && nodeLookup.TryGetValue(focusedNodeId, out var node))
            {
                DismissDetail();
                ShowDetail(node);
            }
            return;
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        if (dir == Vector2.zero) return;

        Vector2 currentPos = Vector2.zero;
        if (focusedNodeId != null && nodePositions.TryGetValue(focusedNodeId, out var currentRT))
            currentPos = currentRT.anchoredPosition;

        string bestId = null;
        float bestScore = float.MaxValue;

        foreach (var kvp in nodePositions)
        {
            if (kvp.Key == focusedNodeId) continue;
            Vector2 toNode = kvp.Value.anchoredPosition - currentPos;
            if (toNode.magnitude < 0.1f) continue;
            float dot = Vector2.Dot(toNode.normalized, dir);
            if (dot <= 0.3f) continue;
            float score = toNode.magnitude / dot;
            if (score < bestScore)
            {
                bestScore = score;
                bestId = kvp.Key;
            }
        }

        if (bestId != null)
        {
            focusedNodeId = bestId;
            if (nodeLookup.TryGetValue(bestId, out var focusedNode))
            {
                DismissDetail();
                ShowDetail(focusedNode);
            }
            UpdateFocusOutline();
        }
    }

    private void UpdateFocusOutline()
    {
        if (focusedNodeId == null || !nodePositions.TryGetValue(focusedNodeId, out var targetRT))
        {
            if (focusOutline != null) focusOutline.SetActive(false);
            return;
        }

        RebuildFocusOutline();
        panTarget = -targetRT.anchoredPosition * currentZoom;
        isPanAnimating = true;
    }

    private void AnimatePan()
    {
        if (!isPanAnimating) return;

        panOffset = Vector2.Lerp(panOffset, panTarget, Time.deltaTime / focusPanDuration);
        graphContainer.anchoredPosition = panOffset;

        if (Vector2.Distance(panOffset, panTarget) < 0.5f)
        {
            panOffset = panTarget;
            graphContainer.anchoredPosition = panOffset;
            isPanAnimating = false;
        }
    }

    private bool IsPointerOverNode()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        var pointerData = new PointerEventData(eventSystem) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerData, results);

        foreach (var r in results)
        {
            if (r.gameObject.GetComponent<Button>() != null && r.gameObject != closeButton?.gameObject)
                return true;
        }
        return false;
    }

    public bool IsOpen => graphPanel != null && graphPanel.activeSelf;

    public void Open()
    {
        graphPanel.SetActive(true);
        if (detailPanel != null)
            detailPanel.SetActive(false);

        panOffset = Vector2.zero;
        currentZoom = 1f;
        graphContainer.anchoredPosition = Vector2.zero;
        graphContainer.localScale = Vector3.one;

        if (graphCanvasGroup == null)
        {
            graphCanvasGroup = graphContainer.GetComponent<CanvasGroup>();
            if (graphCanvasGroup == null)
                graphCanvasGroup = graphContainer.gameObject.AddComponent<CanvasGroup>();
            graphCanvasGroup.blocksRaycasts = true;
            graphCanvasGroup.interactable = true;
        }
        graphCanvasGroup.alpha = 0f;
        graphFadeTarget = 0f;

        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.OpenUI(PlayerState.PAUSED, ClosePanel);

        _ = RefreshAndRender();
    }

    public void OpenToNode(string nodeId)
    {
        Open();
        if (nodeId != null && KnowledgeGraphManager.Inst?.Nodes != null)
        {
            foreach (var n in KnowledgeGraphManager.Inst.Nodes)
            {
                if (n.id == nodeId)
                {
                    focusedNodeId = nodeId;
                    ShowDetail(n);
                    break;
                }
            }
        }
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
        DismissCompletionOverlay();
        DismissDetail();
        graphPanel.SetActive(false);
        isDragging = false;
    }

    private async System.Threading.Tasks.Task RefreshAndRender()
    {
        try
        {
            await KnowledgeGraphManager.Inst.RefreshGraphAsync();
            RenderGraph();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[KnowledgeGraphUI] RefreshAndRender failed: {ex.Message}");
        }
        graphFadeTarget = 1f;
    }

    private void RenderGraph()
    {
        ClearGraph();

        var nodes = KnowledgeGraphManager.Inst?.Nodes;
        if (nodes == null) return;

        var positions = ComputeLayout(nodes);

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var pos in positions.Values)
        {
            if (pos.x < minX) minX = pos.x;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.y < minY) minY = pos.y;
            if (pos.y > maxY) maxY = pos.y;
        }
        Vector2 graphCenter = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);

        nodeLookup.Clear();
        foreach (var node in nodes)
        {
            nodeLookup[node.id] = node;

            var go = Instantiate(nodePrefab, graphContainer);
            go.SetActive(true);
            var rt = go.GetComponent<RectTransform>();

            Vector2 pos = positions[node.id] - graphCenter;
            rt.anchoredPosition = pos;

            var bg = go.GetComponent<Image>();
            if (bg != null)
                bg.color = GetNodeColor(node, nodes);

            var label = go.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = node.title;
                label.alpha = node.status == "locked" ? 0.4f : 1f;
            }

            var btn = go.GetComponent<Button>();
            if (btn != null)
            {
                var captured = node;
                btn.onClick.AddListener(() => OnNodeClicked(captured));
            }

            // Hover effects
            if (bg != null)
            {
                var capturedId = node.id;
                var trigger = go.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry
                    { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
                enterEntry.callback.AddListener((_) => OnNodeHoverEnter(capturedId));
                trigger.triggers.Add(enterEntry);
                var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry
                    { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
                exitEntry.callback.AddListener((_) => OnNodeHoverExit(capturedId));
                trigger.triggers.Add(exitEntry);
            }

            nodePositions[node.id] = rt;
            if (bg != null)
                nodeImages[node.id] = bg;
            spawnedNodes.Add(go);
        }

        BuildNeighborMap(nodes);
        DrawEdges(nodes);
        AutoSelectNode(nodes);
    }

    private void AutoSelectNode(KnowledgeNodeStateDTO[] nodes)
    {
        // Prefer cached selection if it still exists
        if (focusedNodeId != null && nodeLookup.ContainsKey(focusedNodeId))
        {
            if (nodeLookup.TryGetValue(focusedNodeId, out var cached))
            {
                ShowDetail(cached);
                RebuildFocusOutline();

                if (nodePositions.TryGetValue(focusedNodeId, out var rt))
                {
                    panOffset = -rt.anchoredPosition * currentZoom;
                    graphContainer.anchoredPosition = panOffset;
                }
            }
            return;
        }

        // First open: select first node without animating
        KnowledgeNodeStateDTO best = null;
        foreach (var n in nodes)
        {
            if (n.status != "unlocked") continue;
            if (best == null || n.priority == "high")
            {
                best = n;
                if (n.priority == "high") break;
            }
        }
        best ??= nodes.Length > 0 ? nodes[0] : null;

        if (best != null)
        {
            focusedNodeId = best.id;
            ShowDetail(best);
            RebuildFocusOutline();

            // Snap to position without animation
            if (nodePositions.TryGetValue(best.id, out var rt))
            {
                panOffset = -rt.anchoredPosition * currentZoom;
                graphContainer.anchoredPosition = panOffset;
            }
        }
    }

    private void RebuildFocusOutline()
    {
        if (focusedNodeId == null || !nodePositions.TryGetValue(focusedNodeId, out var targetRT))
            return;

        if (focusOutline == null)
        {
            focusOutline = new GameObject("FocusBorder", typeof(RectTransform));
            focusOutline.transform.SetParent(graphContainer, false);
            var names = new[] { "Top", "Bottom", "Left", "Right" };
            for (int i = 0; i < 4; i++)
            {
                var edge = new GameObject(names[i], typeof(RectTransform), typeof(Image));
                edge.transform.SetParent(focusOutline.transform, false);
                edge.GetComponent<Image>().color = focusBorderColor;
            }
        }

        focusOutline.SetActive(true);
        focusOutline.transform.SetAsLastSibling();

        var containerRT = focusOutline.GetComponent<RectTransform>();
        containerRT.anchoredPosition = targetRT.anchoredPosition;
        containerRT.sizeDelta = targetRT.sizeDelta + new Vector2(12, 12);

        var edges = new RectTransform[4];
        for (int i = 0; i < 4; i++)
            edges[i] = focusOutline.transform.GetChild(i).GetComponent<RectTransform>();

        float w = focusBorderWidth;
        edges[0].anchorMin = new Vector2(0, 1); edges[0].anchorMax = new Vector2(1, 1);
        edges[0].pivot = new Vector2(0.5f, 1); edges[0].anchoredPosition = Vector2.zero;
        edges[0].sizeDelta = new Vector2(0, w);
        edges[1].anchorMin = new Vector2(0, 0); edges[1].anchorMax = new Vector2(1, 0);
        edges[1].pivot = new Vector2(0.5f, 0); edges[1].anchoredPosition = Vector2.zero;
        edges[1].sizeDelta = new Vector2(0, w);
        edges[2].anchorMin = new Vector2(0, 0); edges[2].anchorMax = new Vector2(0, 1);
        edges[2].pivot = new Vector2(0, 0.5f); edges[2].anchoredPosition = Vector2.zero;
        edges[2].sizeDelta = new Vector2(w, 0);
        edges[3].anchorMin = new Vector2(1, 0); edges[3].anchorMax = new Vector2(1, 1);
        edges[3].pivot = new Vector2(1, 0.5f); edges[3].anchoredPosition = Vector2.zero;
        edges[3].sizeDelta = new Vector2(w, 0);
    }

    private Dictionary<string, Vector2> ComputeLayout(KnowledgeNodeStateDTO[] nodes)
    {
        var lookup = new Dictionary<string, KnowledgeNodeStateDTO>();
        foreach (var n in nodes)
            lookup[n.id] = n;

        // Assign tiers via longest path from any root
        var tiers = new Dictionary<string, int>();
        int ComputeTier(string id)
        {
            if (tiers.TryGetValue(id, out int cached)) return cached;
            tiers[id] = 0;
            int maxParent = -1;
            if (lookup.TryGetValue(id, out var node) && node.prerequisites != null)
            {
                foreach (var p in node.prerequisites)
                {
                    int pt = ComputeTier(p);
                    if (pt > maxParent) maxParent = pt;
                }
            }
            tiers[id] = maxParent + 1;
            return tiers[id];
        }
        foreach (var n in nodes)
            ComputeTier(n.id);

        // Group by tier
        var tierGroups = new Dictionary<int, List<KnowledgeNodeStateDTO>>();
        foreach (var n in nodes)
        {
            int t = tiers[n.id];
            if (!tierGroups.ContainsKey(t))
                tierGroups[t] = new List<KnowledgeNodeStateDTO>();
            tierGroups[t].Add(n);
        }

        foreach (var group in tierGroups.Values)
            group.Sort((a, b) => string.Compare(a.category ?? "", b.category ?? ""));

        // Initial placement: x by tier, y evenly spaced
        var positions = new Dictionary<string, Vector2>();
        foreach (var kvp in tierGroups)
        {
            int tier = kvp.Key;
            var group = kvp.Value;
            float x = tier * tierSpacing;
            float totalHeight = (group.Count - 1) * nodeSpacing;
            for (int i = 0; i < group.Count; i++)
            {
                float y = -totalHeight / 2f + i * nodeSpacing;
                positions[group[i].id] = new Vector2(x, y);
            }
        }

        // Force-directed adjustment (vertical only, preserve tier x)
        for (int iter = 0; iter < forceIterations; iter++)
        {
            var forces = new Dictionary<string, float>();
            foreach (var n in nodes)
                forces[n.id] = 0f;

            foreach (var kvp in tierGroups)
            {
                var group = kvp.Value;
                for (int i = 0; i < group.Count; i++)
                {
                    for (int j = i + 1; j < group.Count; j++)
                    {
                        float dy = positions[group[i].id].y - positions[group[j].id].y;
                        if (Mathf.Abs(dy) < 1f) dy = 1f;
                        float force = repulsionStrength / (dy * dy) * Mathf.Sign(dy);
                        forces[group[i].id] += force;
                        forces[group[j].id] -= force;
                    }
                }
            }

            foreach (var n in nodes)
            {
                if (n.prerequisites == null) continue;
                foreach (var pid in n.prerequisites)
                {
                    if (!positions.ContainsKey(pid)) continue;
                    float dy = positions[pid].y - positions[n.id].y;
                    forces[n.id] += dy * attractionStrength;
                    forces[pid] -= dy * attractionStrength;
                }
            }

            float damping = 1f - (float)iter / forceIterations;
            foreach (var n in nodes)
            {
                var pos = positions[n.id];
                pos.y += forces[n.id] * damping;
                positions[n.id] = pos;
            }
        }

        // Enforce minimum separation within each tier
        foreach (var kvp in tierGroups)
        {
            var group = kvp.Value;
            group.Sort((a, b) => positions[a.id].y.CompareTo(positions[b.id].y));
            for (int i = 1; i < group.Count; i++)
            {
                float prevY = positions[group[i - 1].id].y;
                float curY = positions[group[i].id].y;
                if (curY - prevY < nodeSpacing)
                {
                    var pos = positions[group[i].id];
                    pos.y = prevY + nodeSpacing;
                    positions[group[i].id] = pos;
                }
            }

            float minY = positions[group[0].id].y;
            float maxY = positions[group[group.Count - 1].id].y;
            float offset = (minY + maxY) / 2f;
            foreach (var n in group)
            {
                var pos = positions[n.id];
                pos.y -= offset;
                positions[n.id] = pos;
            }
        }

        return positions;
    }

    private void BuildNeighborMap(KnowledgeNodeStateDTO[] nodes)
    {
        neighbors.Clear();
        foreach (var n in nodes)
            neighbors[n.id] = new HashSet<string>();

        foreach (var n in nodes)
        {
            if (n.prerequisites == null) continue;
            foreach (var pid in n.prerequisites)
            {
                if (neighbors.ContainsKey(pid))
                    neighbors[pid].Add(n.id);
                if (neighbors.ContainsKey(n.id))
                    neighbors[n.id].Add(pid);
            }
        }
    }

    private void DrawEdges(KnowledgeNodeStateDTO[] nodes)
    {
        edgeRegistry.Clear();
        foreach (var node in nodes)
        {
            if (node.prerequisites == null) continue;

            foreach (var prereqId in node.prerequisites)
            {
                if (!nodePositions.ContainsKey(prereqId) || !nodePositions.ContainsKey(node.id))
                    continue;

                var from = nodePositions[prereqId];
                var to = nodePositions[node.id];
                var edgeGO = DrawLine(from.anchoredPosition, to.anchoredPosition);
                edgeRegistry.Add((prereqId, node.id, edgeGO));
            }
        }
    }

    private GameObject DrawLine(Vector2 from, Vector2 to)
    {
        var parent = new GameObject("Edge", typeof(RectTransform));
        parent.transform.SetParent(graphContainer, false);
        parent.transform.SetAsFirstSibling();

        Vector2 dir = to - from;
        float distance = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        var line = new GameObject("Line", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(parent.transform, false);
        var lineRT = line.GetComponent<RectTransform>();
        var lineImg = line.GetComponent<Image>();
        lineImg.color = edgeColor;
        lineRT.anchoredPosition = from + dir * 0.5f;
        lineRT.sizeDelta = new Vector2(distance, 2f);
        lineRT.localRotation = Quaternion.Euler(0, 0, angle);
        lineRT.pivot = new Vector2(0.5f, 0.5f);

        float arrowSize = 8f;
        Vector2 arrowPos = to - dir.normalized * 35f;
        var arrow = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
        arrow.transform.SetParent(parent.transform, false);
        var arrowRT = arrow.GetComponent<RectTransform>();
        var arrowImg = arrow.GetComponent<Image>();
        arrowImg.color = edgeColor;
        arrowRT.anchoredPosition = arrowPos;
        arrowRT.sizeDelta = new Vector2(arrowSize, arrowSize);
        arrowRT.localRotation = Quaternion.Euler(0, 0, angle - 90f);
        arrowRT.pivot = new Vector2(0.5f, 0.5f);

        spawnedEdges.Add(parent);
        return parent;
    }

    private void HighlightNeighbors(string nodeId)
    {
        highlightedNodeId = nodeId;
        RefreshAllNodeColors();

        bool hasSelection = nodeId != null && neighbors.ContainsKey(nodeId);

        foreach (var (fromId, toId, edgeGO) in edgeRegistry)
        {
            if (edgeGO == null) continue;

            bool connected = hasSelection && (fromId == nodeId || toId == nodeId);
            var highlightColor = connected ? new Color(0.7f, 0.7f, 0.7f, 0.85f) : edgeColor;

            foreach (var img in edgeGO.GetComponentsInChildren<Image>())
                img.color = highlightColor;

            var lineRT = edgeGO.transform.Find("Line")?.GetComponent<RectTransform>();
            if (lineRT != null)
                lineRT.sizeDelta = new Vector2(lineRT.sizeDelta.x, connected ? 3f : 2f);
        }
    }

    private void OnNodeClicked(KnowledgeNodeStateDTO node)
    {
        DismissDetail();
        ShowDetail(node);
        focusedNodeId = node.id;
        UpdateFocusOutline();
    }

    private void ShowDetail(KnowledgeNodeStateDTO node)
    {
        selectedNode = node;
        HighlightNeighbors(node.id);
        if (detailPanel == null) return;

        detailPanel.SetActive(true);
        if (closeButton != null)
            closeButton.transform.SetAsLastSibling();
        detailTitle.text = node.title;
        detailTitle.textWrappingMode = TextWrappingModes.Normal;
        detailTitle.margin = new Vector4(0f, 0f, 30f, 0f);

        var ui = UIConfig.Inst;
        if (ui != null)
        {
            detailTitle.fontSize = ui.Scale(ui.graphDetailTitleSize);
            detailContent.fontSize = ui.Scale(ui.graphDetailContentSize);
            detailStatus.fontSize = ui.Scale(ui.graphDetailStatusSize);
        }

        detailContent.textWrappingMode = TextWrappingModes.Normal;
        detailContent.overflowMode = TextOverflowModes.Overflow;

        // Sidebar shows only short description + prereqs for locked nodes
        string body;
        if (node.status == "locked")
            body = BuildLockedContent(node);
        else
            body = $"<color=#CCCCCC>{node.description}</color>";

        detailContent.text = body;

        string statusLabel = node.type == "adaptive"
            ? node.status switch
            {
                "completed" => "Completed",
                "unlocked" => "Triggered by your trading behavior",
                _ => "Waiting for trigger..."
            }
            : node.status switch
            {
                "completed" => "Completed",
                "unlocked" => "Ready to learn",
                _ => "Locked"
            };

        string catLabel = !string.IsNullOrEmpty(node.category)
            ? node.category.Replace('_', ' ').ToUpper()
            : "";
        detailStatus.textWrappingMode = TextWrappingModes.Normal;
        detailStatus.text = !string.IsNullOrEmpty(catLabel)
            ? $"{catLabel}  ·  {statusLabel}"
            : statusLabel;

        bool isAdaptiveUnlocked = node.type == "adaptive" && node.status == "unlocked";
        bool showCaseyOnCompleted = node.type == "adaptive" && node.status == "completed";

        EnsureCaseyTakeButton();
        if (isAdaptiveUnlocked || showCaseyOnCompleted)
        {
            completeButton.gameObject.SetActive(false);
            caseyTakeButton.gameObject.SetActive(true);
        }
        else
        {
            completeButton.gameObject.SetActive(node.status == "unlocked");
            caseyTakeButton.gameObject.SetActive(false);
        }
    }

    private string BuildLockedContent(KnowledgeNodeStateDTO node)
    {
        string body = $"<color=#888888>{node.description}</color>\n\n";

        if (node.prerequisites != null && node.prerequisites.Length > 0)
        {
            body += "<color=#CCCCCC>Prerequisites:</color>\n";
            foreach (var pid in node.prerequisites)
            {
                bool completed = nodeLookup.TryGetValue(pid, out var prereq) && prereq.status == "completed";
                string icon = completed ? "<color=#6BC96B>✓</color>" : "<color=#E74C3C>✗</color>";
                string title = prereq != null ? prereq.title : pid;
                body += $"  {icon} {title}\n";
            }
            body += "\n";
        }

        if (node.type == "adaptive")
        {
            body += "<color=#CCCCCC>Unlock condition:</color>\n";
            if (!string.IsNullOrEmpty(node.trigger_explanation))
                body += $"<color=#E8A838>{node.trigger_explanation}</color>\n";
            else
                body += "<color=#888888>Triggered by your trading behavior</color>\n";
        }
        else
        {
            body += "<color=#888888>Complete the prerequisites above to unlock.</color>\n";
        }

        if (!string.IsNullOrEmpty(node.reward_mechanic))
            body += $"\n<color=#888888>Unlocks: {FormatMechanic(node.reward_mechanic)}</color>\n";

        return body;
    }

    private string BuildUnlockedContent(KnowledgeNodeStateDTO node)
    {
        string body = "";

        if (node.type == "adaptive" && !string.IsNullOrEmpty(node.trigger_explanation))
        {
            body += $"<color=#E8A838>{node.trigger_explanation}</color>\n\n";
            if (!string.IsNullOrEmpty(node.correct_action))
                body += $"<color=#6BC9D9>{node.correct_action}</color>\n\n";
        }

        body += node.content ?? node.description;

        var featureLabel = ProgressionGates.GetFeatureLabel(node.id);
        if (featureLabel != null)
            body += $"\n\n<color=#D4A0FF>Unlocks: {featureLabel}</color>";
        else if (!string.IsNullOrEmpty(node.reward_mechanic))
            body += $"\n\n<color=#6BC96B>Unlocks: {FormatMechanic(node.reward_mechanic)}</color>";

        return body;
    }

    private static string FormatMechanic(string mechanic)
    {
        return mechanic.Replace('_', ' ')
            .Replace("limit orders", "Limit Orders")
            .Replace("stop loss", "Stop-Loss Orders");
    }

    private void OnCompleteLesson()
    {
        if (selectedNode == null) return;
        if (selectedNode.status != "unlocked")
        {
            Debug.Log($"[KnowledgeGraphUI] Cannot complete '{selectedNode.id}' — status is '{selectedNode.status}', not 'unlocked'");
            return;
        }

        ShowCompletionOverlay(selectedNode, BuildUnlockedContent(selectedNode));
    }

    private void DismissDetail()
    {
        selectedNode = null;
        HighlightNeighbors(null);

        if (detailPanel != null)
            detailPanel.SetActive(false);
        DismissCaseyDialogue();
    }

    // ── Casey's Take dialogue system ──

    private void EnsureCaseyTakeButton()
    {
        if (caseyTakeButton != null) return;
        if (detailPanel == null) return;

        var btnGO = new GameObject("CaseyTakeButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(detailPanel.transform, false);
        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 8f);
        rt.sizeDelta = new Vector2(-20f, 34f);

        var img = btnGO.GetComponent<Image>();
        img.color = new Color(0.15f, 0.35f, 0.3f, 0.95f);

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(btnGO.transform, false);
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.sizeDelta = Vector2.zero;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text = "Casey's Take";
        label.fontSize = UIConfig.Inst != null ? UIConfig.Inst.Scale(UIConfig.Inst.caseyButtonSize) : 16f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.4f, 0.85f, 0.7f);
        EnsureFont(label);

        caseyTakeButton = btnGO.GetComponent<Button>();
        caseyTakeButton.onClick.AddListener(OnCaseyTakeClicked);
    }

    private void EnsureCaseyDialogueBox()
    {
        if (caseyDialogueBox != null) return;
        if (graphPanel == null) return;

        caseyDialogueBox = new GameObject("CaseyDialogueBox", typeof(RectTransform), typeof(Image));
        caseyDialogueBox.transform.SetParent(graphPanel.transform, false);
        caseyDialogueBox.transform.SetAsLastSibling();

        var rt = caseyDialogueBox.GetComponent<RectTransform>();

        float inspectorRight = 1f;
        float inspectorLeft = 0.6f;
        float inspectorPad = 0.02f;
        if (detailPanel != null)
        {
            var dpRT = detailPanel.GetComponent<RectTransform>();
            if (dpRT != null)
            {
                inspectorLeft = dpRT.anchorMin.x;
                inspectorRight = dpRT.anchorMax.x;
            }
        }

        float availableRight = inspectorLeft - inspectorPad;
        float dialogueWidth = availableRight * 0.8f;
        float dialogueLeft = (availableRight - dialogueWidth) / 2f;

        rt.anchorMin = new Vector2(dialogueLeft, 0.02f);
        rt.anchorMax = new Vector2(dialogueLeft + dialogueWidth, 0.22f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        caseyDialogueBox.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.1f, 0.95f);

        var nameGO = new GameObject("Name", typeof(RectTransform));
        nameGO.transform.SetParent(caseyDialogueBox.transform, false);
        var nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0f, 1f);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.pivot = new Vector2(0f, 1f);
        nameRT.anchoredPosition = new Vector2(14f, -8f);
        nameRT.sizeDelta = new Vector2(-28f, 24f);
        caseyNameText = nameGO.AddComponent<TextMeshProUGUI>();
        caseyNameText.text = "Casey";
        caseyNameText.fontSize = UIConfig.Inst != null ? UIConfig.Inst.Scale(UIConfig.Inst.caseyNameSize) : 18f;
        caseyNameText.fontStyle = FontStyles.Bold;
        caseyNameText.color = new Color(0.4f, 0.85f, 0.7f);
        caseyNameText.raycastTarget = false;
        EnsureFont(caseyNameText);

        var bodyGO = new GameObject("Body", typeof(RectTransform));
        bodyGO.transform.SetParent(caseyDialogueBox.transform, false);
        var bodyRT = bodyGO.GetComponent<RectTransform>();
        bodyRT.anchorMin = new Vector2(0f, 0f);
        bodyRT.anchorMax = new Vector2(1f, 1f);
        bodyRT.offsetMin = new Vector2(14f, 28f);
        bodyRT.offsetMax = new Vector2(-14f, -34f);
        caseyBodyText = bodyGO.AddComponent<TextMeshProUGUI>();
        caseyBodyText.fontSize = UIConfig.Inst != null ? UIConfig.Inst.Scale(UIConfig.Inst.caseyBodySize) : 15f;
        caseyBodyText.color = Color.white;
        caseyBodyText.enableWordWrapping = true;
        caseyBodyText.overflowMode = TextOverflowModes.Ellipsis;
        caseyBodyText.raycastTarget = false;
        EnsureFont(caseyBodyText);

        var promptGO = new GameObject("Prompt", typeof(RectTransform));
        promptGO.transform.SetParent(caseyDialogueBox.transform, false);
        var promptRT = promptGO.GetComponent<RectTransform>();
        promptRT.anchorMin = new Vector2(1f, 0f);
        promptRT.anchorMax = new Vector2(1f, 0f);
        promptRT.pivot = new Vector2(1f, 0f);
        promptRT.anchoredPosition = new Vector2(-14f, 6f);
        promptRT.sizeDelta = new Vector2(100f, 20f);
        caseyPromptText = promptGO.AddComponent<TextMeshProUGUI>();
        caseyPromptText.fontSize = UIConfig.Inst != null ? UIConfig.Inst.Scale(UIConfig.Inst.caseyPromptSize) : 13f;
        caseyPromptText.color = new Color(0.5f, 0.5f, 0.55f);
        caseyPromptText.alignment = TextAlignmentOptions.BottomRight;
        caseyPromptText.raycastTarget = false;
        caseyPromptText.text = "";
        EnsureFont(caseyPromptText);

        caseyDialogueBox.SetActive(false);
    }

    private async void OnCaseyTakeClicked()
    {
        if (selectedNode == null) return;
        int entityId = APIBootstrapper.EntityDbId;

        bool isUnlocked = selectedNode.status == "unlocked";

        ShowCompletionOverlay(selectedNode, "<color=#4AD9A4><b>Casey's Take</b></color>\n\nThinking...");
        if (completionGotItButton != null)
            completionGotItButton.interactable = false;

        string body;

        if (entityId <= 0 || selectedNode.type != "adaptive")
        {
            body = BuildUnlockedContent(selectedNode);
        }
        else
        {
            try
            {
                var resp = await KnowledgeGraphAPI.GetNodeContent(entityId, selectedNode.id);
                var content = resp != null && !string.IsNullOrEmpty(resp.content)
                    ? resp.content
                    : selectedNode.content ?? selectedNode.description;

                body = "<color=#4AD9A4><b>Casey's Take</b></color>\n\n";
                if (!string.IsNullOrEmpty(selectedNode.trigger_explanation))
                    body += $"<color=#E8A838>{selectedNode.trigger_explanation}</color>\n\n";
                if (!string.IsNullOrEmpty(selectedNode.correct_action))
                    body += $"<color=#6BC9D9>{selectedNode.correct_action}</color>\n\n";
                body += content;

                var featureLabel = ProgressionGates.GetFeatureLabel(selectedNode.id);
                if (featureLabel != null)
                    body += $"\n\n<color=#D4A0FF>Unlocks: {featureLabel}</color>";
                else if (!string.IsNullOrEmpty(selectedNode.reward_mechanic))
                    body += $"\n\n<color=#6BC96B>Unlocks: {FormatMechanic(selectedNode.reward_mechanic)}</color>";
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[KnowledgeGraphUI] Casey's Take failed: {ex.Message}");
                body = BuildUnlockedContent(selectedNode);
            }
        }

        if (!completionOverlayActive) return;

        completionBodyText.text = body;
        if (completionScrollRect != null)
            completionScrollRect.verticalNormalizedPosition = 1f;
        if (completionGotItButton != null)
            completionGotItButton.interactable = true;
    }

    private void HandleCaseyInput()
    {
        if (caseyDialogueBox == null || !caseyDialogueBox.activeSelf) return;

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
        {
            if (caseyIsTyping)
            {
                caseyBodyText.maxVisibleCharacters = caseyTotalChars;
                caseyIsTyping = false;
                caseyPromptText.text = (caseyLineIndex < caseyLines.Length - 1) ? "E >" : "E to close";
            }
            else
            {
                caseyLineIndex++;
                if (caseyLineIndex >= caseyLines.Length)
                {
                    DismissCaseyDialogue();
                }
                else
                {
                    BeginCaseyTypewriter(caseyLines[caseyLineIndex]);
                    caseyPromptText.text = "";
                }
            }
        }
    }

    private void UpdateCaseyTypewriter()
    {
        if (!caseyIsTyping || caseyBodyText == null) return;

        caseyCharAccum += Time.deltaTime * CaseyCharsPerSec;
        int visible = Mathf.Min(caseyTotalChars, (int)caseyCharAccum);
        caseyBodyText.maxVisibleCharacters = visible;

        if (visible >= caseyTotalChars)
        {
            caseyIsTyping = false;
            if (caseyLines != null)
                caseyPromptText.text = (caseyLineIndex < caseyLines.Length - 1) ? "E >" : "E to close";
        }
    }

    private void BeginCaseyTypewriter(string text)
    {
        caseyBodyText.text = text;
        caseyBodyText.ForceMeshUpdate();
        caseyTotalChars = caseyBodyText.textInfo.characterCount;
        caseyBodyText.maxVisibleCharacters = 0;
        caseyCharAccum = 0f;
        caseyIsTyping = true;
    }

    private void DismissCaseyDialogue()
    {
        if (caseyDialogueBox != null)
            caseyDialogueBox.SetActive(false);
        caseyLines = null;
        caseyIsTyping = false;
    }

    // ── Completion Overlay ──
    // Uses explicit anchor-based positioning (no VLG/CSF) so TMP always knows its dimensions.

    private void EnsureCompletionOverlay()
    {
        if (completionOverlay != null) return;
        if (graphPanel == null) return;

        var ui = UIConfig.Inst;
        float titleSize = ui != null ? ui.Scale(ui.graphDetailTitleSize) : 22f;
        float bodySize = ui != null ? ui.Scale(ui.graphDetailContentSize) : 16f;
        float statusSize = ui != null ? ui.Scale(ui.graphDetailStatusSize) : 13f;

        const float cardW = 700f, cardH = 520f;
        const float pad = 32f, topPad = 24f, botPad = 24f;
        const float catH = 22f, titleH = 35f, divH = 1f, btnH = 40f;
        const float gap = 10f;

        float catTop = topPad;
        float titleTop = catTop + catH + gap;
        float divTop = titleTop + titleH + gap;
        float viewTop = divTop + divH + gap;
        float btnBot = botPad;
        float viewBot = btnBot + btnH + gap;

        completionOverlay = new GameObject("CompletionOverlay", typeof(RectTransform), typeof(CanvasGroup));
        completionOverlay.transform.SetParent(graphPanel.transform, false);
        var rootRT = completionOverlay.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.sizeDelta = Vector2.zero;
        rootRT.anchoredPosition = Vector2.zero;
        completionOverlayGroup = completionOverlay.GetComponent<CanvasGroup>();
        completionOverlayGroup.alpha = 0f;
        completionOverlayGroup.blocksRaycasts = false;

        var dimGO = new GameObject("DimBackground", typeof(RectTransform), typeof(Image));
        dimGO.transform.SetParent(completionOverlay.transform, false);
        var dimRT = dimGO.GetComponent<RectTransform>();
        dimRT.anchorMin = Vector2.zero;
        dimRT.anchorMax = Vector2.one;
        dimRT.sizeDelta = Vector2.zero;
        dimGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        var cardGO = new GameObject("Card", typeof(RectTransform), typeof(Image));
        cardGO.transform.SetParent(completionOverlay.transform, false);
        completionCardRT = cardGO.GetComponent<RectTransform>();
        completionCardRT.anchorMin = new Vector2(0.5f, 0.5f);
        completionCardRT.anchorMax = new Vector2(0.5f, 0.5f);
        completionCardRT.sizeDelta = new Vector2(cardW, cardH);
        completionCardRT.anchoredPosition = Vector2.zero;
        cardGO.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.97f);

        // Category — anchored to top, stretches width
        var catGO = new GameObject("Category", typeof(RectTransform));
        catGO.transform.SetParent(cardGO.transform, false);
        var catRT = catGO.GetComponent<RectTransform>();
        catRT.anchorMin = new Vector2(0, 1);
        catRT.anchorMax = new Vector2(1, 1);
        catRT.pivot = new Vector2(0.5f, 1);
        catRT.anchoredPosition = new Vector2(0, -catTop);
        catRT.sizeDelta = new Vector2(-pad * 2, catH);
        completionCategoryText = catGO.AddComponent<TextMeshProUGUI>();
        completionCategoryText.fontSize = statusSize;
        completionCategoryText.fontStyle = FontStyles.Italic;
        completionCategoryText.alignment = TextAlignmentOptions.Center;
        completionCategoryText.color = new Color(0.91f, 0.66f, 0.22f);
        completionCategoryText.raycastTarget = false;
        EnsureFont(completionCategoryText);

        // Title — below category
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(cardGO.transform, false);
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 1);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.pivot = new Vector2(0.5f, 1);
        titleRT.anchoredPosition = new Vector2(0, -titleTop);
        titleRT.sizeDelta = new Vector2(-pad * 2, titleH);
        completionTitleText = titleGO.AddComponent<TextMeshProUGUI>();
        completionTitleText.fontSize = titleSize;
        completionTitleText.fontStyle = FontStyles.Bold;
        completionTitleText.alignment = TextAlignmentOptions.Center;
        completionTitleText.color = Color.white;
        completionTitleText.raycastTarget = false;
        EnsureFont(completionTitleText);

        // Divider
        var divGO = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        divGO.transform.SetParent(cardGO.transform, false);
        var divRT = divGO.GetComponent<RectTransform>();
        divRT.anchorMin = new Vector2(0, 1);
        divRT.anchorMax = new Vector2(1, 1);
        divRT.pivot = new Vector2(0.5f, 1);
        divRT.anchoredPosition = new Vector2(0, -divTop);
        divRT.sizeDelta = new Vector2(-pad * 2, divH);
        divGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

        // Scroll viewport — fills space between divider and button
        var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewportGO.transform.SetParent(cardGO.transform, false);
        var viewportRT = viewportGO.GetComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = new Vector2(pad, viewBot);
        viewportRT.offsetMax = new Vector2(-pad, -viewTop);

        // Body text inside viewport — stretches width, grows height with content
        var bodyGO = new GameObject("Body", typeof(RectTransform));
        bodyGO.transform.SetParent(viewportGO.transform, false);
        var bodyRT = bodyGO.GetComponent<RectTransform>();
        bodyRT.anchorMin = new Vector2(0, 1);
        bodyRT.anchorMax = new Vector2(1, 1);
        bodyRT.pivot = new Vector2(0.5f, 1);
        bodyRT.anchoredPosition = Vector2.zero;
        bodyRT.sizeDelta = new Vector2(0, 0);
        completionBodyText = bodyGO.AddComponent<TextMeshProUGUI>();
        completionBodyText.fontSize = bodySize;
        completionBodyText.alignment = TextAlignmentOptions.TopLeft;
        completionBodyText.color = new Color(0.85f, 0.85f, 0.88f);
        completionBodyText.enableWordWrapping = true;
        completionBodyText.overflowMode = TextOverflowModes.Overflow;
        completionBodyText.raycastTarget = true;
        EnsureFont(completionBodyText);
        var bodyCsf = bodyGO.AddComponent<ContentSizeFitter>();
        bodyCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        bodyCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        completionScrollRect = viewportGO.AddComponent<ScrollRect>();
        completionScrollRect.viewport = viewportRT;
        completionScrollRect.content = bodyRT;
        completionScrollRect.vertical = true;
        completionScrollRect.horizontal = false;
        completionScrollRect.movementType = ScrollRect.MovementType.Clamped;
        completionScrollRect.scrollSensitivity = 30f;

        // Vertical scrollbar
        var scrollbarGO = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarGO.transform.SetParent(cardGO.transform, false);
        var sbRT = scrollbarGO.GetComponent<RectTransform>();
        sbRT.anchorMin = new Vector2(1, 0);
        sbRT.anchorMax = new Vector2(1, 1);
        sbRT.pivot = new Vector2(1, 0.5f);
        sbRT.offsetMin = new Vector2(-8f, viewBot);
        sbRT.offsetMax = new Vector2(0f, -viewTop);
        scrollbarGO.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.5f);

        var handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGO.transform.SetParent(scrollbarGO.transform, false);
        var handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.anchorMin = Vector2.zero;
        handleRT.anchorMax = Vector2.one;
        handleRT.sizeDelta = Vector2.zero;
        handleGO.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.55f, 0.7f);

        var scrollbar = scrollbarGO.GetComponent<Scrollbar>();
        scrollbar.handleRect = handleRT;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleGO.GetComponent<Image>();
        completionScrollRect.verticalScrollbar = scrollbar;
        completionScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        // Got It button — anchored bottom center
        var btnGO = new GameObject("GotItButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(cardGO.transform, false);
        var btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.5f, 0);
        btnRT.anchorMax = new Vector2(0.5f, 0);
        btnRT.pivot = new Vector2(0.5f, 0);
        btnRT.anchoredPosition = new Vector2(0, btnBot);
        btnRT.sizeDelta = new Vector2(180, btnH);
        btnGO.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.9f, 1f);

        var btnLabelGO = new GameObject("Label", typeof(RectTransform));
        btnLabelGO.transform.SetParent(btnGO.transform, false);
        var btnLabelRT = btnLabelGO.GetComponent<RectTransform>();
        btnLabelRT.anchorMin = Vector2.zero;
        btnLabelRT.anchorMax = Vector2.one;
        btnLabelRT.sizeDelta = Vector2.zero;
        btnLabelRT.offsetMin = Vector2.zero;
        btnLabelRT.offsetMax = Vector2.zero;
        var btnLabel = btnLabelGO.AddComponent<TextMeshProUGUI>();
        btnLabel.text = "Got it";
        btnLabel.fontSize = ui != null ? ui.Scale(16f) : 16f;
        btnLabel.alignment = TextAlignmentOptions.Center;
        btnLabel.color = Color.white;
        EnsureFont(btnLabel);

        completionGotItButton = btnGO.GetComponent<Button>();
        completionGotItButton.onClick.AddListener(OnCompletionOverlayDismissed);

        completionOverlay.SetActive(false);
    }

    private void ShowCompletionOverlay(KnowledgeNodeStateDTO node, string bodyContent)
    {
        EnsureCompletionOverlay();

        pendingCompletionNodeId = node.id;

        completionOverlay.SetActive(true);
        completionOverlay.transform.SetAsLastSibling();
        completionOverlayGroup.alpha = 1f;
        completionOverlayGroup.blocksRaycasts = true;
        completionOverlayActive = true;

        string catLabel = !string.IsNullOrEmpty(node.category)
            ? node.category.Replace('_', ' ').ToUpper()
            : "";
        completionCategoryText.text = catLabel;
        completionTitleText.text = node.title;
        completionBodyText.text = bodyContent;

        if (completionScrollRect != null)
            completionScrollRect.verticalNormalizedPosition = 1f;
    }

    private void DismissCompletionOverlay()
    {
        if (completionOverlay == null || !completionOverlayActive) return;
        completionOverlayGroup.alpha = 0f;
        completionOverlayGroup.blocksRaycasts = false;
        completionOverlay.SetActive(false);
        completionOverlayActive = false;
        pendingCompletionNodeId = null;
    }

    private async void OnCompletionOverlayDismissed()
    {
        if (pendingCompletionNodeId == null)
        {
            DismissCompletionOverlay();
            return;
        }

        var nodeId = pendingCompletionNodeId;
        bool alreadyCompleted = nodeLookup.TryGetValue(nodeId, out var n) && n.status == "completed";
        DismissCompletionOverlay();

        if (alreadyCompleted)
        {
            selectedNode = null;
            if (detailPanel != null) detailPanel.SetActive(false);
            return;
        }

        Debug.Log($"[KnowledgeGraphUI] Completing node: {nodeId}");
        if (completeButton != null) completeButton.gameObject.SetActive(false);
        if (caseyTakeButton != null) caseyTakeButton.gameObject.SetActive(false);

        await KnowledgeGraphManager.Inst.CompleteNodeAsync(nodeId);

        selectedNode = null;
        if (detailPanel != null)
            detailPanel.SetActive(false);

        RenderGraph();
    }

    private void HandleCompletionOverlayInput()
    {
        if (!completionOverlayActive) return;

        if (Input.GetKeyDown(KeyCode.Escape))
            DismissCompletionOverlay();
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            OnCompletionOverlayDismissed();
    }

    private string[] SplitIntoDialogueLines(string content)
    {
        var paragraphs = content.Split(new[] { "\n\n", "\r\n\r\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        if (paragraphs.Length <= 1)
        {
            var sentences = content.Split(new[] { ". " }, System.StringSplitOptions.RemoveEmptyEntries);
            if (sentences.Length <= 2) return new[] { content };
            var lines = new List<string>();
            for (int i = 0; i < sentences.Length; i += 2)
            {
                var chunk = sentences[i].TrimEnd('.') + ".";
                if (i + 1 < sentences.Length)
                    chunk += " " + sentences[i + 1].TrimEnd('.') + ".";
                lines.Add(chunk.Trim());
            }
            return lines.ToArray();
        }
        return paragraphs;
    }

    private Color GetNodeColor(KnowledgeNodeStateDTO node, KnowledgeNodeStateDTO[] allNodes)
    {
        if (node.type == "adaptive")
        {
            return node.status switch
            {
                "completed" => adaptiveCompletedColor,
                "unlocked" => adaptiveUnlockedColor,
                _ => ArePrereqsMet(node, allNodes) ? adaptiveReadyColor : adaptiveLockedColor
            };
        }

        return node.status switch
        {
            "completed" => completedColor,
            "unlocked" => unlockedColor,
            _ => lockedColor
        };
    }

    private bool ArePrereqsMet(KnowledgeNodeStateDTO node, KnowledgeNodeStateDTO[] allNodes)
    {
        if (node.prerequisites == null || node.prerequisites.Length == 0)
            return true;

        foreach (var prereqId in node.prerequisites)
        {
            bool found = false;
            foreach (var n in allNodes)
            {
                if (n.id == prereqId)
                {
                    if (n.status != "completed")
                        return false;
                    found = true;
                    break;
                }
            }
            if (!found) return false;
        }
        return true;
    }

    private void OnNodeHoverEnter(string nodeId)
    {
        hoveredNodeId = nodeId;
        RefreshNodeColor(nodeId);
    }

    private void OnNodeHoverExit(string nodeId)
    {
        if (hoveredNodeId == nodeId)
            hoveredNodeId = null;
        RefreshNodeColor(nodeId);
    }

    private void RefreshNodeColor(string nodeId)
    {
        if (!nodeImages.TryGetValue(nodeId, out var img)) return;
        if (!nodeLookup.TryGetValue(nodeId, out var node)) return;

        var nodes = KnowledgeGraphManager.Inst?.Nodes;
        if (nodes == null) return;

        Color color = GetNodeColor(node, nodes);

        // Dim if another node is highlighted and this one isn't a neighbor
        if (highlightedNodeId != null && nodeId != highlightedNodeId)
        {
            bool isNeighbor = neighbors.TryGetValue(highlightedNodeId, out var nset) && nset.Contains(nodeId);
            if (!isNeighbor)
            {
                color.r *= 0.4f;
                color.g *= 0.4f;
                color.b *= 0.4f;
            }
        }

        // Lighten if hovered
        if (hoveredNodeId == nodeId)
            color = Color.Lerp(color, Color.white, 0.2f);

        img.color = color;
    }

    private void RefreshAllNodeColors()
    {
        foreach (var nodeId in nodeImages.Keys)
            RefreshNodeColor(nodeId);
    }

    private static TMP_FontAsset _cachedFont;
    private static void EnsureFont(TMP_Text tmp)
    {
        if (_cachedFont == null)
            _cachedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (_cachedFont != null)
            tmp.font = _cachedFont;
    }

    private void ClearGraph()
    {
        foreach (var go in spawnedNodes) Destroy(go);
        foreach (var go in spawnedEdges) Destroy(go);
        if (focusOutline != null) { Destroy(focusOutline); focusOutline = null; }
        spawnedNodes.Clear();
        spawnedEdges.Clear();
        nodePositions.Clear();
        nodeLookup.Clear();
        nodeImages.Clear();
        neighbors.Clear();
        edgeRegistry.Clear();
        highlightedNodeId = null;
        hoveredNodeId = null;
    }
}
