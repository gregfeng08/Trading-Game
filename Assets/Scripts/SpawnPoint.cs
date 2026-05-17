using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnPointName;

    void Start()
    {
        if (SceneTransitionManager.Inst == null) return;

        string pending = SceneTransitionManager.Inst.PendingSpawnPoint;
        if (string.IsNullOrEmpty(pending)) return;
        if (spawnPointName != pending) return;

        SceneTransitionManager.Inst.ConsumePendingSpawnPoint();

        GameObject player = GameObject.Find("== Player ==");
        if (player == null) return;

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.SetPositionAndRotation(transform.position, transform.rotation);
        if (cc != null) cc.enabled = true;

        PlayerMovement pm = player.GetComponent<PlayerMovement>();
        if (pm != null) pm.SnapCamera();
    }
}
