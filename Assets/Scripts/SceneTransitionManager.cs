using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Inst { get; private set; }

    public string PendingSpawnPoint { get; private set; }

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadScene(string sceneName, string spawnPoint = null)
    {
        PendingSpawnPoint = spawnPoint;
        SceneManager.LoadScene(sceneName);
    }

    public void ConsumePendingSpawnPoint()
    {
        PendingSpawnPoint = null;
    }
}
