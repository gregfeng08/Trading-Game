using UnityEngine;

public class SpawnZone : MonoBehaviour
{
    [SerializeField] private float radius = 20f;
    [SerializeField] private float weightMultiplier = 2f;

    public float Radius => radius;
    public float WeightMultiplier => weightMultiplier;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.25f);
        Gizmos.DrawSphere(transform.position, radius);
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
