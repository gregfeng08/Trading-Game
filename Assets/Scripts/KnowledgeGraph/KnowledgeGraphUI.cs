using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
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

    private bool isDragging;
    private Vector2 lastMousePos;
    private Vector2 panOffset;
    private float currentZoom = 1f;

    // Arrow key navigation
    private string focusedNodeId;
    private GameObject focusOutline;
    private Vector2 panTarget;
    private bool isPanAnimating;

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
            if (selectedNode != null && selectedNode.status == "unlocked")
                OnCompleteLesson();
            else if (focusedNodeId != null && nodeLookup.TryGetValue(focusedNodeId, out var node))
            {
                DismissDetail();
                ShowDetail(node);
            }
            return;
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (selectedNode != null)
                DismissDetail();
            else
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

    public void Open()
    {
        graphPanel.SetActive(true);
        if (detailPanel != null)
            detailPanel.SetActive(false);

        panOffset = Vector2.zero;
        currentZoom = 1f;
        graphContainer.anchoredPosition = Vector2.zero;
        graphContainer.localScale = Vector3.one;

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
        DismissDetail();
        graphPanel.SetActive(false);
        isDragging = false;
    }

    private async System.Threading.Tasks.Task RefreshAndRender()
    {
        await KnowledgeGraphManager.Inst.RefreshGraphAsync();
        RenderGraph();
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

            nodePositions[node.id] = rt;
            if (bg != null) nodeImages[node.id] = bg;
            spawnedNodes.Add(go);
        }

        BuildNeighborMap(nodes);
        DrawEdges(nodes);
        StartCoroutine(AutoSelectDeferred(nodes));
    }

    private System.Collections.IEnumerator AutoSelectDeferred(KnowledgeNodeStateDTO[] nodes)
    {
        yield return null;
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
        var go = new GameObject("Edge", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(graphContainer, false);
        go.transform.SetAsFirstSibling();

        var rt = go.GetComponent<RectTransform>();
        var img = go.GetComponent<Image>();
        img.color = edgeColor;

        Vector2 dir = to - from;
        float distance = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        rt.anchoredPosition = from + dir * 0.5f;
        rt.sizeDelta = new Vector2(distance, 2f);
        rt.localRotation = Quaternion.Euler(0, 0, angle);
        rt.pivot = new Vector2(0.5f, 0.5f);

        spawnedEdges.Add(go);
        return go;
    }

    private void HighlightNeighbors(string nodeId)
    {
        var nodes = KnowledgeGraphManager.Inst?.Nodes;
        if (nodes == null) return;

        bool hasSelection = nodeId != null && neighbors.ContainsKey(nodeId);
        var neighborSet = hasSelection ? neighbors[nodeId] : null;

        foreach (var n in nodes)
        {
            if (!nodeImages.TryGetValue(n.id, out var img)) continue;
            var baseColor = GetNodeColor(n, nodes);

            if (hasSelection && n.id != nodeId && (neighborSet == null || !neighborSet.Contains(n.id)))
            {
                baseColor.r *= 0.4f;
                baseColor.g *= 0.4f;
                baseColor.b *= 0.4f;
            }

            img.color = baseColor;
        }

        foreach (var (fromId, toId, edgeGO) in edgeRegistry)
        {
            if (edgeGO == null) continue;
            var img = edgeGO.GetComponent<Image>();
            var rt = edgeGO.GetComponent<RectTransform>();

            bool connected = hasSelection && (fromId == nodeId || toId == nodeId);
            if (connected)
            {
                if (img != null) img.color = new Color(0.7f, 0.7f, 0.7f, 0.85f);
                if (rt != null) rt.sizeDelta = new Vector2(rt.sizeDelta.x, 3f);
            }
            else
            {
                if (img != null) img.color = edgeColor;
                if (rt != null) rt.sizeDelta = new Vector2(rt.sizeDelta.x, 2f);
            }
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
        detailTitle.text = node.title;
        detailTitle.textWrappingMode = TextWrappingModes.Normal;

        detailContent.textWrappingMode = TextWrappingModes.Normal;
        detailContent.overflowMode = TextOverflowModes.Overflow;

        string body = "";

        if (node.status == "locked")
        {
            body += BuildLockedContent(node);
        }
        else
        {
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
        }

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

        completeButton.gameObject.SetActive(node.status == "unlocked");
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

    private static string FormatMechanic(string mechanic)
    {
        return mechanic.Replace('_', ' ')
            .Replace("limit orders", "Limit Orders")
            .Replace("stop loss", "Stop-Loss Orders");
    }

    private async void OnCompleteLesson()
    {
        if (selectedNode != null && selectedNode.status == "unlocked")
        {
            await KnowledgeGraphManager.Inst.CompleteNodeAsync(selectedNode.id);
            RenderGraph();
        }
        selectedNode = null;

        if (detailPanel != null)
            detailPanel.SetActive(false);
    }

    private void DismissDetail()
    {
        selectedNode = null;
        HighlightNeighbors(null);

        if (detailPanel != null)
            detailPanel.SetActive(false);
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
    }
}
