using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class LeylineGraph : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private float tileSize = 10f;
    [SerializeField] private float laneOffset = 1.5f;
    [SerializeField] private string walkableLayerName = "NPC walkable";

    [Header("Debug")]
    [SerializeField] private bool drawGizmos;

    private Dictionary<Vector2Int, Vector3> tiles;
    private Dictionary<Vector2Int, List<Vector2Int>> adjacency;
    private List<Vector2Int> tileList;
    private int walkableLayer;

    public static LeylineGraph Instance { get; private set; }
    public float LaneOffset => laneOffset;

    void Awake()
    {
        Instance = this;
        BuildGraph();
    }

    private void BuildGraph()
    {
        tiles = new Dictionary<Vector2Int, Vector3>();
        adjacency = new Dictionary<Vector2Int, List<Vector2Int>>();

        walkableLayer = LayerMask.NameToLayer(walkableLayerName);
        if (walkableLayer < 0)
        {
            Debug.LogError($"LeylineGraph: Layer '{walkableLayerName}' not found.");
            return;
        }

        var allRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (var r in allRenderers)
        {
            if (r.gameObject.layer != walkableLayer) continue;

            Vector3 center = r.bounds.center;
            Vector3 size = r.bounds.size;
            float y = center.y;

            bool wideX = size.x / tileSize > 1.5f;
            bool wideZ = size.z / tileSize > 1.5f;

            if (wideX || wideZ)
            {
                float halfStep = tileSize * 0.5f;
                Vector3 offsetA = wideX ? new Vector3(-halfStep, 0f, 0f) : new Vector3(0f, 0f, -halfStep);
                Vector3 offsetB = wideX ? new Vector3(halfStep, 0f, 0f) : new Vector3(0f, 0f, halfStep);

                RegisterCell(center + offsetA, y);
                RegisterCell(center + offsetB, y);
            }
            else
            {
                RegisterCell(center, y);
            }
        }

        Debug.Log($"LeylineGraph: Scanned {tiles.Count} cells from renderers on '{walkableLayerName}' layer.");
        tileList = new List<Vector2Int>(tiles.Keys);

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var tile in tileList)
        {
            var neighbors = new List<Vector2Int>();
            foreach (var dir in directions)
            {
                Vector2Int neighbor = tile + dir;
                if (tiles.ContainsKey(neighbor))
                    neighbors.Add(neighbor);
            }
            adjacency[tile] = neighbors;
        }

        int isolated = 0, deadEnds = 0;
        foreach (var kvp in adjacency)
        {
            if (kvp.Value.Count == 0) isolated++;
            else if (kvp.Value.Count == 1) deadEnds++;
        }

        Debug.Log($"LeylineGraph: {tiles.Count} tiles, {CountEdges()} edges. Dead ends: {deadEnds}, Isolated: {isolated}");
    }

    private void RegisterCell(Vector3 samplePoint, float y)
    {
        Vector2Int gridPos = WorldToGrid(samplePoint);
        Vector3 cellCenter = new Vector3(
            (gridPos.x + 0.5f) * tileSize,
            y,
            (gridPos.y + 0.5f) * tileSize);
        tiles.TryAdd(gridPos, cellCenter);
    }

    private int CountEdges()
    {
        int count = 0;
        foreach (var kvp in adjacency)
            count += kvp.Value.Count;
        return count / 2;
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / tileSize),
            Mathf.FloorToInt(worldPos.z / tileSize)
        );
    }

    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        if (tiles.TryGetValue(gridPos, out Vector3 pos))
            return pos;
        return new Vector3(gridPos.x * tileSize, 0f, gridPos.y * tileSize);
    }

    public bool HasTile(Vector2Int gridPos) => tiles.ContainsKey(gridPos);

    public List<Vector2Int> GetNeighbors(Vector2Int gridPos)
    {
        if (adjacency.TryGetValue(gridPos, out var neighbors))
            return neighbors;
        return new List<Vector2Int>();
    }

    public Vector3 GetLanePosition(Vector2Int from, Vector2Int to)
    {
        Vector3 fromWorld = GridToWorld(from);
        Vector3 toWorld = GridToWorld(to);
        Vector3 dir = (toWorld - fromWorld).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, dir);

        Vector3 offset = toWorld + right * laneOffset;

        if (NavMesh.SamplePosition(offset, out NavMeshHit hit, laneOffset + 1f, NavMesh.AllAreas))
            return hit.position;

        return toWorld;
    }

    public Vector2Int GetNearestTile(Vector3 worldPos)
    {
        Vector2Int gridPos = WorldToGrid(worldPos);
        if (tiles.ContainsKey(gridPos))
            return gridPos;

        float bestDist = float.MaxValue;
        Vector2Int best = gridPos;
        foreach (var tile in tileList)
        {
            float dist = (tile - gridPos).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = tile;
            }
        }
        return best;
    }

    public Vector2Int GetRandomTile()
    {
        if (tileList == null || tileList.Count == 0)
            return Vector2Int.zero;
        return tileList[Random.Range(0, tileList.Count)];
    }

    public Vector2Int GetRandomTileAtDistance(Vector2Int from, int minDist, int maxDist)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            var candidate = GetRandomTile();
            int dist = Mathf.Abs(candidate.x - from.x) + Mathf.Abs(candidate.y - from.y);
            if (dist >= minDist && dist <= maxDist)
                return candidate;
        }
        return GetRandomTile();
    }

    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int end)
    {
        if (!tiles.ContainsKey(start) || !tiles.ContainsKey(end))
            return null;
        if (start == end)
            return new List<Vector2Int> { start };

        var openSet = new PriorityQueue(tiles.Count);
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float>();
        var visited = new HashSet<Vector2Int>();

        gScore[start] = 0;
        openSet.Enqueue(start, Heuristic(start, end));

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();

            if (current == end)
                return ReconstructPath(cameFrom, current);

            if (!visited.Add(current))
                continue;

            if (!adjacency.TryGetValue(current, out var neighbors))
                continue;

            float currentG = gScore[current];

            foreach (var neighbor in neighbors)
            {
                if (visited.Contains(neighbor))
                    continue;

                float tentativeG = currentG + 1f;
                if (!gScore.TryGetValue(neighbor, out float existingG) || tentativeG < existingG)
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    openSet.Enqueue(neighbor, tentativeG + Heuristic(neighbor, end));
                }
            }
        }

        return null;
    }

    private float Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        var path = new List<Vector2Int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || tiles == null) return;

        foreach (var kvp in tiles)
        {
            int neighborCount = adjacency.ContainsKey(kvp.Key) ? adjacency[kvp.Key].Count : 0;
            // Red = isolated (0 neighbors), magenta = dead end (1), cyan = normal
            Gizmos.color = neighborCount == 0 ? Color.red : neighborCount == 1 ? Color.magenta : Color.cyan;
            Gizmos.DrawWireSphere(kvp.Value + Vector3.up * 0.5f, 0.3f);
        }

        // Draw leyline edges (full line between adjacent tiles)
        Gizmos.color = Color.yellow;
        var drawn = new HashSet<long>();
        foreach (var kvp in adjacency)
        {
            Vector3 from = GridToWorld(kvp.Key) + Vector3.up * 0.5f;
            foreach (var neighbor in kvp.Value)
            {
                long edgeKey = ((long)Mathf.Min(kvp.Key.GetHashCode(), neighbor.GetHashCode()) << 32)
                             | (long)Mathf.Max(kvp.Key.GetHashCode(), neighbor.GetHashCode());
                if (!drawn.Add(edgeKey)) continue;

                Vector3 to = GridToWorld(neighbor) + Vector3.up * 0.5f;
                Gizmos.DrawLine(from, to);

                // Draw lane offset positions as small spheres
                Vector3 dir = (to - from).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, dir);
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(to + right * laneOffset, 0.15f);
                Gizmos.color = new Color(1f, 0.5f, 0f);
                Gizmos.DrawSphere(to - right * laneOffset, 0.15f);
                Gizmos.color = Color.yellow;
            }
        }
    }

    private class PriorityQueue
    {
        private List<(Vector2Int node, float priority)> heap;

        public int Count => heap.Count;

        public PriorityQueue(int capacity)
        {
            heap = new List<(Vector2Int, float)>(capacity);
        }

        public void Enqueue(Vector2Int node, float priority)
        {
            heap.Add((node, priority));
            BubbleUp(heap.Count - 1);
        }

        public Vector2Int Dequeue()
        {
            var result = heap[0].node;
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);
            if (heap.Count > 0)
                BubbleDown(0);
            return result;
        }

        private void BubbleUp(int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (heap[i].priority >= heap[parent].priority) break;
                (heap[i], heap[parent]) = (heap[parent], heap[i]);
                i = parent;
            }
        }

        private void BubbleDown(int i)
        {
            int count = heap.Count;
            while (true)
            {
                int smallest = i;
                int left = 2 * i + 1;
                int right = 2 * i + 2;
                if (left < count && heap[left].priority < heap[smallest].priority)
                    smallest = left;
                if (right < count && heap[right].priority < heap[smallest].priority)
                    smallest = right;
                if (smallest == i) break;
                (heap[i], heap[smallest]) = (heap[smallest], heap[i]);
                i = smallest;
            }
        }
    }
}
