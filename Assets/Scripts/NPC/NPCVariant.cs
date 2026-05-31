using UnityEngine;

[System.Serializable]
public class NPCVariant
{
    public GameObject prefab;
    [Tooltip("analyst, broker, trader, anchor, or empty for random")]
    public string npcType;
    [Range(0.1f, 10f)]
    public float weight = 1f;
}
