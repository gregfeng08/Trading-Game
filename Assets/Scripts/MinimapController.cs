using UnityEngine;
using UnityEngine.UI;

public class MinimapController : MonoBehaviour
{
    [SerializeField] private Camera minimapCamera;
    [SerializeField] private RawImage minimapImage;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Color iconColor = new Color(1f, 0.92f, 0.016f, 1f);
    [SerializeField] private float iconSize = 1.5f;
    [SerializeField] private float cameraHeight = 40f;
    [SerializeField] private float orthoSize = 30f;
    [SerializeField] private Color backgroundColor = new Color(0.15f, 0.15f, 0.2f, 1f);

    void Start()
    {
        if (minimapCamera != null)
        {
            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = orthoSize;
            minimapCamera.backgroundColor = backgroundColor;
        }
    }

    void LateUpdate()
    {
        if (minimapCamera == null || playerTransform == null) return;

        Vector3 playerPos = playerTransform.position;
        minimapCamera.transform.position = new Vector3(playerPos.x, playerPos.y + cameraHeight, playerPos.z);
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }
}
