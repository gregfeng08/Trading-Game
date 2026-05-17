using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.API.DTO;

public class PlayerObjectivesUI : MonoBehaviour
{
    public static PlayerObjectivesUI Inst { get; private set; }

    [SerializeField] private Transform objectivesList;
    [SerializeField] private GameObject objectiveItemPrefab;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color newItemColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private float highlightDuration = 3f;

    private readonly List<ObjectiveEntry> entries = new();

    private class ObjectiveEntry
    {
        public string id;
        public GameObject go;
        public TMP_Text text;
        public float highlightTimer;
        public bool isNew;
    }

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(this); return; }
        Inst = this;
    }

    void OnEnable()
    {
        if (KnowledgeGraphManager.Inst != null)
            KnowledgeGraphManager.Inst.OnHighPriorityUnlock += OnKnowledgeUnlocked;
    }

    void OnDisable()
    {
        if (KnowledgeGraphManager.Inst != null)
            KnowledgeGraphManager.Inst.OnHighPriorityUnlock -= OnKnowledgeUnlocked;
    }

    void Update()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (!e.isNew) continue;

            e.highlightTimer -= Time.deltaTime;
            if (e.highlightTimer <= 0f)
            {
                e.isNew = false;
                if (e.text != null)
                    e.text.color = normalColor;
            }
        }
    }

    public void AddObjective(string id, string text)
    {
        if (entries.Exists(e => e.id == id)) return;

        var go = objectiveItemPrefab != null
            ? Instantiate(objectiveItemPrefab, objectivesList)
            : CreateDefaultItem();

        var tmp = go.GetComponentInChildren<TMP_Text>();
        if (tmp != null)
        {
            tmp.text = $"- {text}";
            tmp.color = newItemColor;
        }

        go.transform.SetAsLastSibling();

        entries.Add(new ObjectiveEntry
        {
            id = id,
            go = go,
            text = tmp,
            highlightTimer = highlightDuration,
            isNew = true
        });
    }

    public void CompleteObjective(string id)
    {
        var entry = entries.Find(e => e.id == id);
        if (entry == null) return;

        if (entry.text != null)
        {
            entry.text.fontStyle = FontStyles.Strikethrough;
            entry.text.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        }
    }

    public void RemoveObjective(string id)
    {
        var entry = entries.Find(e => e.id == id);
        if (entry == null) return;

        entries.Remove(entry);
        if (entry.go != null)
            Destroy(entry.go);
    }

    private void OnKnowledgeUnlocked(UnlockedNodeDTO node)
    {
        AddObjective($"kg_{node.node_id}", $"New insight: {node.title}");
    }

    private GameObject CreateDefaultItem()
    {
        var go = new GameObject("Objective", typeof(RectTransform));
        go.transform.SetParent(objectivesList, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Left;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 25);
        return go;
    }
}
