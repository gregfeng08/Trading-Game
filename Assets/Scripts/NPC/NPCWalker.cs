using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCWalker : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.8f;
    [SerializeField] private float minIdleTime = 1f;
    [SerializeField] private float maxIdleTime = 3f;

    [Header("Leyline Wandering")]
    [SerializeField] private int minWanderDistance = 3;
    [SerializeField] private int maxWanderDistance = 10;

    [Header("Lifecycle")]
    [SerializeField] private bool despawnOnArrival;

    [Header("Animation Tuning")]
    [SerializeField] private float animSpeedMultiplier = 1f;

    private NavMeshAgent agent;
    private Animator animator;
    private float idleTimer;
    private bool isIdling;

    // Manual route mode
    private Transform[] route;
    private int routeIndex;
    private bool routeForward = true;
    private bool despawn;

    // Leyline mode
    private bool useLeyline;
    private List<Vector2Int> leylinePath;
    private int leylineIndex;
    private Vector2Int currentTile;
    private bool waitingForPath;

    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

    public void Init(Transform[] routeWaypoints, bool shouldDespawn = false)
    {
        useLeyline = false;
        route = routeWaypoints;
        despawn = shouldDespawn;
    }

    public void InitLeyline(bool shouldDespawn = false)
    {
        useLeyline = true;
        despawn = shouldDespawn;
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();

        if (animator != null)
            animator.applyRootMotion = false;

        agent.speed = walkSpeed;
        agent.angularSpeed = 180f;
        agent.acceleration = 6f;
        agent.stoppingDistance = 0.5f;
        agent.autoBraking = true;
    }

    void Start()
    {
        if (useLeyline)
        {
            StartLeylineWander();
        }
        else if (route != null && route.Length > 0)
        {
            GoToCurrentWaypoint();
        }
        else if (LeylineGraph.Instance != null)
        {
            useLeyline = true;
            StartLeylineWander();
        }
    }

    void Update()
    {
        if (isIdling)
        {
            idleTimer -= Time.deltaTime;
            if (idleTimer <= 0f)
            {
                isIdling = false;
                if (useLeyline)
                    AdvanceLeyline();
                else
                    AdvanceRoute();
            }
        }
        else if (agent.pathPending)
        {
            waitingForPath = true;
        }
        else if (waitingForPath && agent.hasPath)
        {
            waitingForPath = false;
        }
        else if (!waitingForPath && agent.hasPath && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (useLeyline)
                OnLeylineSegmentReached();
            else
                StartIdle();
        }

        if (animator != null)
        {
            bool walking = agent.velocity.sqrMagnitude > 0.01f;
            animator.SetBool(IsWalkingHash, walking);
            animator.speed = walking ? animSpeedMultiplier : 1f;
        }
    }

    #region Leyline Navigation

    private void StartLeylineWander()
    {
        var graph = LeylineGraph.Instance;
        if (graph == null)
        {
            Debug.LogWarning("NPCWalker: No LeylineGraph found, falling back to idle.");
            return;
        }

        currentTile = graph.GetNearestTile(transform.position);
        PickNewLeylineDestination();
    }

    private void PickNewLeylineDestination()
    {
        var graph = LeylineGraph.Instance;
        if (graph == null) return;

        Vector2Int destination = graph.GetRandomTileAtDistance(currentTile, minWanderDistance, maxWanderDistance);
        leylinePath = graph.FindPath(currentTile, destination);

        if (leylinePath == null || leylinePath.Count < 2)
        {
            PickNewLeylineDestination();
            return;
        }

        leylineIndex = 1;
        SetLeylineDestination();
    }

    private void SetLeylineDestination()
    {
        var graph = LeylineGraph.Instance;
        Vector2Int from = leylinePath[leylineIndex - 1];
        Vector2Int to = leylinePath[leylineIndex];
        Vector3 target = graph.GetLanePosition(from, to);
        agent.SetDestination(target);
        waitingForPath = true;
    }

    private void OnLeylineSegmentReached()
    {
        if (leylinePath == null) return;

        currentTile = leylinePath[leylineIndex];

        if (leylineIndex >= leylinePath.Count - 1)
        {
            if (despawn)
            {
                Destroy(gameObject);
                return;
            }
            StartIdle();
            return;
        }

        leylineIndex++;
        SetLeylineDestination();
    }

    private void AdvanceLeyline()
    {
        PickNewLeylineDestination();
    }

    #endregion

    #region Manual Route Navigation

    private void AdvanceRoute()
    {
        if (route == null || route.Length == 0) return;

        if (despawn)
        {
            routeIndex++;
            if (routeIndex >= route.Length)
            {
                Destroy(gameObject);
                return;
            }
        }
        else
        {
            if (routeForward)
            {
                routeIndex++;
                if (routeIndex >= route.Length)
                {
                    routeIndex = route.Length - 2;
                    routeForward = false;
                }
            }
            else
            {
                routeIndex--;
                if (routeIndex < 0)
                {
                    routeIndex = 1;
                    routeForward = true;
                }
            }
        }

        routeIndex = Mathf.Clamp(routeIndex, 0, route.Length - 1);
        GoToCurrentWaypoint();
    }

    private void GoToCurrentWaypoint()
    {
        agent.SetDestination(route[routeIndex].position);
        waitingForPath = true;
    }

    #endregion

    private void StartIdle()
    {
        isIdling = true;
        idleTimer = Random.Range(minIdleTime, maxIdleTime);
        agent.ResetPath();
    }
}
