using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Game.API;
using Game.API.DTO;

public class KnowledgeGraphManager : MonoBehaviour
{
    public static KnowledgeGraphManager Inst { get; private set; }

    public event Action<UnlockedNodeDTO> OnHighPriorityUnlock;
    public event Action<int> OnPendingCountChanged;
    public event Action<string> OnMechanicUnlocked;
    public event Action OnInitialized;
    public event Action OnGraphRefreshed;

    public KnowledgeNodeStateDTO[] Nodes { get; private set; }
    public int PendingUnlockedCount { get; private set; }
    public bool IsInitialized { get; private set; }

    private Queue<UnlockedNodeDTO> pendingPopups = new();
    private HashSet<string> unlockedMechanics = new();

    private bool debugInProgress;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.F9) && !debugInProgress)
            _ = DebugCompleteAllNodesAsync();
    }

    public async Task InitializeAsync()
    {
        if (APIBootstrapper.EntityDbId < 0) return;

        try
        {
            await KnowledgeGraphAPI.Initialize(APIBootstrapper.EntityDbId);
            await RefreshGraphAsync();
            await RefreshUnlocksAsync();
            IsInitialized = true;
            ProgressionGates.Initialize();
            OnInitialized?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[KnowledgeGraph] Init failed: {ex.Message}");
        }
    }

    public async Task RefreshGraphAsync()
    {
        if (APIBootstrapper.EntityDbId < 0) return;

        try
        {
            var resp = await KnowledgeGraphAPI.GetGraph(APIBootstrapper.EntityDbId);
            if (resp.status == "ok" && resp.nodes != null)
            {
                Nodes = resp.nodes;
                RecountPending();
                OnGraphRefreshed?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[KnowledgeGraph] Refresh failed: {ex.Message}");
        }
    }

    public async Task CheckTriggersAsync()
    {
        if (APIBootstrapper.EntityDbId < 0 || !IsInitialized) return;

        try
        {
            var resp = await KnowledgeGraphAPI.CheckTriggers(APIBootstrapper.EntityDbId);

            if (resp.status == "ok" && resp.newly_unlocked != null)
            {
                foreach (var node in resp.newly_unlocked)
                {
                    pendingPopups.Enqueue(node);
                    OnHighPriorityUnlock?.Invoke(node);
                }
            }

            await RefreshGraphAsync();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[KnowledgeGraph] Trigger check failed: {ex.Message}");
        }
    }

    public async Task CompleteNodeAsync(string nodeId)
    {
        if (APIBootstrapper.EntityDbId < 0) return;

        try
        {
            var resp = await KnowledgeGraphAPI.CompleteNode(APIBootstrapper.EntityDbId, nodeId);
            if (!string.IsNullOrEmpty(resp.reward_mechanic))
            {
                unlockedMechanics.Add(resp.reward_mechanic);
                OnMechanicUnlocked?.Invoke(resp.reward_mechanic);
            }
            await RefreshGraphAsync();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[KnowledgeGraph] Complete node failed: {ex.Message}");
        }
    }

    public async Task DebugCompleteAllNodesAsync()
    {
        if (APIBootstrapper.EntityDbId < 0 || debugInProgress) return;
        debugInProgress = true;

        try
        {
            Debug.Log("[KnowledgeGraph] DEBUG: Completing all nodes...");
            var resp = await KnowledgeGraphAPI.DebugCompleteAll(APIBootstrapper.EntityDbId);
            Debug.Log($"[KnowledgeGraph] DEBUG: Completed {resp.completed_count} nodes");
            await RefreshGraphAsync();
            await RefreshUnlocksAsync();
            ProgressionGates.Evaluate();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KnowledgeGraph] DEBUG complete all failed: {ex.Message}");
        }
        finally
        {
            debugInProgress = false;
        }
    }

    public bool HasMechanic(string mechanic) => unlockedMechanics.Contains(mechanic);

    public bool IsNodeCompleted(string nodeId)
    {
        if (Nodes == null) return false;
        foreach (var n in Nodes)
            if (n.id == nodeId && n.status == "completed") return true;
        return false;
    }

    public List<string> GetPrerequisiteChain(string nodeId)
    {
        var chain = new List<string>();
        var visited = new HashSet<string>();
        CollectPrerequisites(nodeId, chain, visited);
        return chain;
    }

    private void CollectPrerequisites(string nodeId, List<string> chain, HashSet<string> visited)
    {
        if (!visited.Add(nodeId)) return;
        if (Nodes == null) return;

        foreach (var node in Nodes)
        {
            if (node.id != nodeId) continue;
            if (node.prerequisites != null)
            {
                foreach (var prereq in node.prerequisites)
                    CollectPrerequisites(prereq, chain, visited);
            }
            break;
        }
        chain.Add(nodeId);
    }

    public IReadOnlyCollection<string> UnlockedMechanics => unlockedMechanics;

    public async Task RefreshUnlocksAsync()
    {
        if (APIBootstrapper.EntityDbId < 0) return;

        try
        {
            var resp = await KnowledgeGraphAPI.GetUnlocks(APIBootstrapper.EntityDbId);
            if (resp.status == "ok" && resp.mechanics != null)
            {
                unlockedMechanics = new HashSet<string>(resp.mechanics);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[KnowledgeGraph] Unlocks refresh failed: {ex.Message}");
        }
    }

    public UnlockedNodeDTO DequeuePopup()
    {
        return pendingPopups.Count > 0 ? pendingPopups.Dequeue() : null;
    }

    public bool HasPendingPopups() => pendingPopups.Count > 0;

    private void RecountPending()
    {
        int count = 0;
        if (Nodes != null)
        {
            foreach (var n in Nodes)
            {
                if (n.status == "unlocked")
                    count++;
            }
        }

        if (count != PendingUnlockedCount)
        {
            PendingUnlockedCount = count;
            OnPendingCountChanged?.Invoke(count);
        }
    }
}
