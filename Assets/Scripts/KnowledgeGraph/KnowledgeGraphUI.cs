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

    private List<GameObject> spawnedNodes = new();
    private List<GameObject> spawnedEdges = new();
    private KnowledgeNodeStateDTO selectedNode;
    private Dictionary<string, RectTransform> nodePositions = new();

    private bool isDragging;
    private Vector2 lastMousePos;
    private Vector2 panOffset;
    private float currentZoom = 1f;

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

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        HandlePan();
        HandleZoom();
    }

    private void HandlePan()
    {
        if (Input.GetMouseButtonDown(0) && !IsPointerOverNode())
        {
            isDragging = true;
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
            PlayerStateController.Inst.SetState(PlayerState.PAUSED);

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
        DismissDetail();
        graphPanel.SetActive(false);
        isDragging = false;

        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.MOVING);
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

        // Compute bounding box center to place graph at origin (center of container)
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var n in nodes)
        {
            float px = n.position.x * graphScale;
            float py = -n.position.y * graphScale;
            if (px < minX) minX = px;
            if (px > maxX) maxX = px;
            if (py < minY) minY = py;
            if (py > maxY) maxY = py;
        }

        Vector2 graphCenter = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);

        foreach (var node in nodes)
        {
            var go = Instantiate(nodePrefab, graphContainer);
            var rt = go.GetComponent<RectTransform>();

            Vector2 pos = new Vector2(
                node.position.x * graphScale - graphCenter.x,
                -node.position.y * graphScale - graphCenter.y
            );
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
                btn.interactable = node.status != "locked";
            }

            nodePositions[node.id] = rt;
            spawnedNodes.Add(go);
        }

        DrawEdges(nodes);
    }

    private void DrawEdges(KnowledgeNodeStateDTO[] nodes)
    {
        foreach (var node in nodes)
        {
            if (node.prerequisites == null) continue;

            foreach (var prereqId in node.prerequisites)
            {
                if (!nodePositions.ContainsKey(prereqId) || !nodePositions.ContainsKey(node.id))
                    continue;

                var from = nodePositions[prereqId];
                var to = nodePositions[node.id];
                DrawLine(from.anchoredPosition, to.anchoredPosition);
            }
        }
    }

    private void DrawLine(Vector2 from, Vector2 to)
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
    }

    private void OnNodeClicked(KnowledgeNodeStateDTO node)
    {
        if (node.status == "locked") return;
        DismissDetail();
        ShowDetail(node);
    }

    private void ShowDetail(KnowledgeNodeStateDTO node)
    {
        selectedNode = node;
        if (detailPanel == null) return;

        detailPanel.SetActive(true);
        detailTitle.text = node.title;
        detailTitle.textWrappingMode = TextWrappingModes.Normal;

        detailContent.textWrappingMode = TextWrappingModes.Normal;
        detailContent.overflowMode = TextOverflowModes.Overflow;

        string body = "";

        if (node.type == "adaptive" && !string.IsNullOrEmpty(node.trigger_explanation))
        {
            body += $"<color=#E8A838>{node.trigger_explanation}</color>\n\n";

            if (!string.IsNullOrEmpty(node.correct_action))
                body += $"<color=#6BC9D9>{node.correct_action}</color>\n\n";
        }

        body += node.content ?? node.description;

        if (!string.IsNullOrEmpty(node.reward_mechanic))
            body += $"\n\n<color=#6BC96B>Unlocks: {FormatMechanic(node.reward_mechanic)}</color>";
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
        spawnedNodes.Clear();
        spawnedEdges.Clear();
        nodePositions.Clear();
    }
}
