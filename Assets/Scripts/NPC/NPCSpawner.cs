using UnityEngine;
using System.Collections.Generic;

public class NPCSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private NPCVariant[] npcVariants;
    [Tooltip("Legacy fallback: used if npcVariants is empty")]
    [SerializeField] private GameObject[] npcPrefabs;

    [Header("Routes (Legacy - manual waypoint mode)")]
    [Tooltip("Each child is a route. Each route's children are sequential waypoints.")]
    [SerializeField] private Transform routeParent;

    [Header("Persistent NPCs (patrol back and forth)")]
    [SerializeField] private int persistentPerRoute = 1;

    [Header("Leyline Wanderers (weighted spawn)")]
    [SerializeField] private int maxLeylineWanderers = 12;
    [SerializeField] private float leylineSpawnInterval = 3.5f;

    [Header("Commuter NPCs (edge-to-edge through-traffic)")]
    [SerializeField] private int maxCommuters = 6;
    [SerializeField] private float commuterSpawnInterval = 6f;
    [SerializeField] private int commuterMinEdgeDistance = 5;

    [Header("Pedestrian NPCs (walk route once and despawn)")]
    [SerializeField] private int maxPedestrians = 8;
    [SerializeField] private float spawnInterval = 5f;

    [Header("Density Control")]
    [SerializeField] private float minSpawnSpacing = 8f;
    [SerializeField] private int maxNearbyForSpawn = 3;
    [SerializeField] private int maxSpawnRetries = 5;

    [Header("Phase Population Scaling")]
    [SerializeField] private float preMarketScale = 0.6f;
    [SerializeField] private float dayScale = 1.0f;
    [SerializeField] private float postMarketScale = 0.3f;

    private Transform[][] routes;
    private int activePedestrians;
    private int activeLeylineWanderers;
    private int activeCommuters;
    private float spawnTimer;
    private float leylineSpawnTimer;
    private float commuterSpawnTimer;
    private readonly List<Transform> trackedNPCs = new List<Transform>();

    void Start()
    {
        BuildRoutes();
        SpawnPersistent();
        SpawnInitialPopulation();
    }

    private bool HasPrefabs => (npcVariants != null && npcVariants.Length > 0)
                              || (npcPrefabs != null && npcPrefabs.Length > 0);

    private (GameObject prefab, string npcType) PickWeightedPrefab()
    {
        if (npcVariants != null && npcVariants.Length > 0)
        {
            float total = 0f;
            foreach (var v in npcVariants) total += v.weight;
            float roll = Random.Range(0f, total);
            float accum = 0f;
            foreach (var v in npcVariants)
            {
                accum += v.weight;
                if (roll <= accum)
                    return (v.prefab, v.npcType);
            }
            var last = npcVariants[^1];
            return (last.prefab, last.npcType);
        }
        return (npcPrefabs[Random.Range(0, npcPrefabs.Length)], null);
    }

    void Update()
    {
        if (!HasPrefabs) return;

        float popScale = GetPhasePopulationScale();

        if (LeylineGraph.Instance != null)
        {
            leylineSpawnTimer -= Time.deltaTime;
            int effectiveMaxWanderers = Mathf.RoundToInt(maxLeylineWanderers * popScale);
            if (leylineSpawnTimer <= 0f && activeLeylineWanderers < effectiveMaxWanderers)
            {
                SpawnLeylineWanderer();
                leylineSpawnTimer = leylineSpawnInterval + Random.Range(-1f, 1f);
            }

            commuterSpawnTimer -= Time.deltaTime;
            int effectiveMaxCommuters = Mathf.RoundToInt(maxCommuters * popScale);
            if (commuterSpawnTimer <= 0f && activeCommuters < effectiveMaxCommuters)
            {
                SpawnCommuter();
                commuterSpawnTimer = commuterSpawnInterval + Random.Range(-2f, 2f);
            }
        }

        if (routes != null && routes.Length > 0)
        {
            spawnTimer -= Time.deltaTime;
            int effectiveMaxPedestrians = Mathf.RoundToInt(maxPedestrians * popScale);
            if (spawnTimer <= 0f && activePedestrians < effectiveMaxPedestrians)
            {
                SpawnPedestrian();
                spawnTimer = spawnInterval + Random.Range(-1.5f, 1.5f);
            }
        }
    }

    private void SpawnInitialPopulation()
    {
        if (!HasPrefabs) return;

        float popScale = GetPhasePopulationScale();

        if (LeylineGraph.Instance != null)
        {
            int targetWanderers = Mathf.RoundToInt(maxLeylineWanderers * popScale);
            for (int i = 0; i < targetWanderers; i++)
                SpawnLeylineWanderer();

            int targetCommuters = Mathf.RoundToInt(maxCommuters * popScale);
            for (int i = 0; i < targetCommuters; i++)
                SpawnCommuter();
        }

        if (routes != null && routes.Length > 0)
        {
            int targetPedestrians = Mathf.RoundToInt(maxPedestrians * popScale);
            for (int i = 0; i < targetPedestrians; i++)
                SpawnPedestrian();
        }
    }

    private float GetPhasePopulationScale()
    {
        if (GamePhaseManager.Inst == null) return 1f;
        return GamePhaseManager.Inst.CurrentPhase switch
        {
            GamePhase.PreMarket => preMarketScale,
            GamePhase.Day => dayScale,
            GamePhase.PostMarket => postMarketScale,
            _ => 1f
        };
    }

    private void BuildRoutes()
    {
        if (routeParent == null) return;

        routes = new Transform[routeParent.childCount][];
        for (int r = 0; r < routeParent.childCount; r++)
        {
            var routeObj = routeParent.GetChild(r);
            routes[r] = new Transform[routeObj.childCount];
            for (int w = 0; w < routeObj.childCount; w++)
                routes[r][w] = routeObj.GetChild(w);
        }
    }

    private void SpawnPersistent()
    {
        if (!HasPrefabs) return;
        if (routes == null) return;

        for (int r = 0; r < routes.Length; r++)
        {
            if (routes[r].Length < 2) continue;

            for (int i = 0; i < persistentPerRoute; i++)
            {
                int startIndex = Random.Range(0, routes[r].Length);
                var (prefab, npcType) = PickWeightedPrefab();
                var npc = Instantiate(
                    prefab,
                    routes[r][startIndex].position,
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                npc.name = $"NPC_Persistent_R{r}_{i}";

                var walker = EnsureWalker(npc);
                walker.Init(routes[r], shouldDespawn: false);
                SetupBark(npc, npcType);
            }
        }
    }

    private void SpawnLeylineWanderer()
    {
        var graph = LeylineGraph.Instance;
        if (graph == null) return;

        Vector3 spawnPos = Vector3.zero;
        bool found = false;

        for (int attempt = 0; attempt < maxSpawnRetries; attempt++)
        {
            Vector2Int candidate = graph.GetWeightedRandomTile();
            Vector3 candidatePos = graph.GridToWorld(candidate);

            if (!IsTooCrowded(candidatePos))
            {
                spawnPos = candidatePos;
                found = true;
                break;
            }
        }

        if (!found)
        {
            spawnPos = graph.GridToWorld(graph.GetWeightedRandomTile());
        }

        var (prefab, npcType) = PickWeightedPrefab();
        var npc = Instantiate(
            prefab,
            spawnPos,
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        npc.name = $"NPC_Leyline_{Time.frameCount}";

        var walker = EnsureWalker(npc);
        walker.InitLeyline(shouldDespawn: true);
        SetupBark(npc, npcType);

        activeLeylineWanderers++;
        trackedNPCs.Add(npc.transform);
        var tracker = npc.AddComponent<DespawnTracker>();
        tracker.Init(this, DespawnTracker.NPCType.Leyline);
    }

    private void SpawnCommuter()
    {
        var graph = LeylineGraph.Instance;
        if (graph == null) return;

        var edgeTiles = graph.GetEdgeTiles();
        if (edgeTiles == null || edgeTiles.Count < 2) return;

        Vector2Int startTile = graph.GetRandomEdgeTile();
        Vector3 startPos = graph.GridToWorld(startTile);

        if (IsTooCrowded(startPos))
            return;

        Vector2Int destTile = graph.GetRandomEdgeTileFarFrom(startTile, commuterMinEdgeDistance);

        if (graph.FindPath(startTile, destTile) == null)
            return;

        var (prefab, npcType) = PickWeightedPrefab();
        var npc = Instantiate(
            prefab,
            startPos,
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        npc.name = $"NPC_Commuter_{Time.frameCount}";

        var walker = EnsureWalker(npc);
        walker.InitLeyline(destTile, shouldDespawn: true);
        SetupBark(npc, npcType);

        activeCommuters++;
        trackedNPCs.Add(npc.transform);
        var tracker = npc.AddComponent<DespawnTracker>();
        tracker.Init(this, DespawnTracker.NPCType.Commuter);
    }

    private void SpawnPedestrian()
    {
        if (routes == null || routes.Length == 0) return;

        var route = routes[Random.Range(0, routes.Length)];
        if (route.Length < 2) return;

        bool reverse = Random.value > 0.5f;
        Transform[] orderedRoute;
        if (reverse)
        {
            orderedRoute = new Transform[route.Length];
            for (int i = 0; i < route.Length; i++)
                orderedRoute[i] = route[route.Length - 1 - i];
        }
        else
        {
            orderedRoute = route;
        }

        var (prefab, npcType) = PickWeightedPrefab();
        var npc = Instantiate(
            prefab,
            orderedRoute[0].position,
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        npc.name = $"NPC_Pedestrian_{Time.frameCount}";

        var walker = EnsureWalker(npc);
        walker.Init(orderedRoute, shouldDespawn: true);
        SetupBark(npc, npcType);

        activePedestrians++;
        var tracker = npc.AddComponent<DespawnTracker>();
        tracker.Init(this, DespawnTracker.NPCType.Pedestrian);
    }

    private bool IsTooCrowded(Vector3 position)
    {
        trackedNPCs.RemoveAll(t => t == null);

        int nearby = 0;
        foreach (var npc in trackedNPCs)
        {
            if (Vector3.Distance(npc.position, position) < minSpawnSpacing)
            {
                nearby++;
                if (nearby >= maxNearbyForSpawn)
                    return true;
            }
        }
        return false;
    }

    public void OnNPCDespawned(DespawnTracker.NPCType type)
    {
        switch (type)
        {
            case DespawnTracker.NPCType.Pedestrian:
                activePedestrians--;
                break;
            case DespawnTracker.NPCType.Leyline:
                activeLeylineWanderers--;
                break;
            case DespawnTracker.NPCType.Commuter:
                activeCommuters--;
                break;
        }
    }

    private NPCWalker EnsureWalker(GameObject npc)
    {
        if (npc.GetComponent<UnityEngine.AI.NavMeshAgent>() == null)
            npc.AddComponent<UnityEngine.AI.NavMeshAgent>();

        var walker = npc.GetComponent<NPCWalker>();
        if (walker == null)
            walker = npc.AddComponent<NPCWalker>();

        return walker;
    }

    private static readonly HashSet<string> InteractableTypes = new()
        { "analyst", "broker", "trader", "historian" };

    private static readonly Dictionary<string, string> NpcDisplayNames = new()
    {
        { "analyst", "The Analyst" },
        { "broker", "The Broker" },
        { "trader", "The Floor Trader" },
        { "historian", "The Historian" }
    };

    private void SetupBark(GameObject npc, string npcType = null)
    {
        var bark = npc.AddComponent<NPCBark>();
        bark.Init(npcType: npcType);

        if (!string.IsNullOrEmpty(npcType) && InteractableTypes.Contains(npcType))
        {
            var interaction = npc.AddComponent<NPCInteraction>();
            string displayName = NpcDisplayNames.TryGetValue(npcType, out var name) ? name : npcType;
            interaction.Init(npcType, displayName);
        }
    }
}

public class DespawnTracker : MonoBehaviour
{
    public enum NPCType { Pedestrian, Leyline, Commuter }

    private NPCSpawner spawner;
    private NPCType npcType;

    public void Init(NPCSpawner owner, NPCType type)
    {
        spawner = owner;
        npcType = type;
    }

    void OnDestroy()
    {
        if (spawner != null)
            spawner.OnNPCDespawned(npcType);
    }
}
