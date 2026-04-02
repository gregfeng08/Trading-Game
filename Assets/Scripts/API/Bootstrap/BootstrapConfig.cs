using UnityEngine;

[CreateAssetMenu(menuName = "Game/Bootstrap Config")]
public class BootstrapConfig : ScriptableObject
{
    [Header("API")]
    public string baseUrl = "http://127.0.0.1:5000";

    [Header("Market Data")]
    public string startDate = "2010-01-01";   // YYYY-MM-DD
    public string endDate = "2012-12-31";   // YYYY-MM-DD
    public int topN = 50;

    [Header("Bootstrap Steps")]
    public bool initDb = true;
    public bool loadTickers = true;
    public bool registerEntity = true;

    [Header("Entity")]
    public string entity_id = "player_001";
    public string entity_type = "player";
    public string display_name = "Player";
}
