using UnityEngine;

public class GroundIndicator : MonoBehaviour
{
    [SerializeField] private float groundOffset = 0.02f;
    [SerializeField] private float maxRayDistance = 20f;
    [SerializeField] private LayerMask groundMask = ~0;

    void LateUpdate()
    {
        Vector3 origin = transform.parent.position + Vector3.up * 0.5f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxRayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 pos = transform.position;
            pos.y = hit.point.y + groundOffset;
            transform.position = pos;
        }
    }
}
