using UnityEngine;

public class NPCSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject[] npcPrefabs;

    [Header("Routes (Legacy - manual waypoint mode)")]
    [Tooltip("Each child is a route. Each route's children are sequential waypoints.")]
    [SerializeField] private Transform routeParent;

    [Header("Persistent NPCs (patrol back and forth)")]
    [SerializeField] private int persistentPerRoute = 1;

    [Header("Leyline Wanderers")]
    [SerializeField] private int maxLeylineWanderers = 10;
    [SerializeField] private float leylineSpawnInterval = 4f;

    [Header("Pedestrian NPCs (walk route once and despawn)")]
    [SerializeField] private int maxPedestrians = 8;
    [SerializeField] private float spawnInterval = 5f;

    private Transform[][] routes;
    private int activePedestrians;
    private int activeLeylineWanderers;
    private float spawnTimer;
    private float leylineSpawnTimer;

    void Start()
    {
        BuildRoutes();
        SpawnPersistent();
    }

    void Update()
    {
        if (npcPrefabs == null || npcPrefabs.Length == 0) return;

        // Leyline wanderers
        if (LeylineGraph.Instance != null)
        {
            leylineSpawnTimer -= Time.deltaTime;
            if (leylineSpawnTimer <= 0f && activeLeylineWanderers < maxLeylineWanderers)
            {
                SpawnLeylineWanderer();
                leylineSpawnTimer = leylineSpawnInterval + Random.Range(-1f, 1f);
            }
        }

        // Legacy pedestrians
        if (routes != null && routes.Length > 0)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f && activePedestrians < maxPedestrians)
            {
                SpawnPedestrian();
                spawnTimer = spawnInterval + Random.Range(-1.5f, 1.5f);
            }
        }
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
        if (npcPrefabs == null || npcPrefabs.Length == 0) return;
        if (routes == null) return;

        for (int r = 0; r < routes.Length; r++)
        {
            if (routes[r].Length < 2) continue;

            for (int i = 0; i < persistentPerRoute; i++)
            {
                int startIndex = Random.Range(0, routes[r].Length);
                var npc = Instantiate(
                    npcPrefabs[Random.Range(0, npcPrefabs.Length)],
                    routes[r][startIndex].position,
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                npc.name = $"NPC_Persistent_R{r}_{i}";

                var walker = EnsureWalker(npc);
                walker.Init(routes[r], shouldDespawn: false);
            }
        }
    }

    private void SpawnLeylineWanderer()
    {
        var graph = LeylineGraph.Instance;
        if (graph == null) return;

        Vector2Int spawnTile = graph.GetRandomTile();
        Vector3 spawnPos = graph.GridToWorld(spawnTile);

        var npc = Instantiate(
            npcPrefabs[Random.Range(0, npcPrefabs.Length)],
            spawnPos,
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        npc.name = $"NPC_Leyline_{Time.frameCount}";

        var walker = EnsureWalker(npc);
        walker.InitLeyline(shouldDespawn: true);

        activeLeylineWanderers++;
        var tracker = npc.AddComponent<DespawnTracker>();
        tracker.Init(this, DespawnTracker.NPCType.Leyline);
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

        var npc = Instantiate(
            npcPrefabs[Random.Range(0, npcPrefabs.Length)],
            orderedRoute[0].position,
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        npc.name = $"NPC_Pedestrian_{Time.frameCount}";

        var walker = EnsureWalker(npc);
        walker.Init(orderedRoute, shouldDespawn: true);

        activePedestrians++;
        var tracker = npc.AddComponent<DespawnTracker>();
        tracker.Init(this, DespawnTracker.NPCType.Pedestrian);
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
}

public class DespawnTracker : MonoBehaviour
{
    public enum NPCType { Pedestrian, Leyline }

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
