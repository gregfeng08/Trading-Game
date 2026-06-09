using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class MinimapController : MonoBehaviour
{
    public static MinimapController Inst { get; private set; }

    [Header("References (optional — auto-creates if null)")]
    [SerializeField] private Camera minimapCamera;
    [SerializeField] private RawImage minimapImage;
    [SerializeField] private Transform playerTransform;

    [Header("Existing RenderTexture (optional — creates one if null)")]
    [SerializeField] private RenderTexture renderTexture;

    [Header("Camera Settings")]
    [SerializeField] private float cameraHeight = 50f;
    [SerializeField] private float orthoSize = 35f;
    [SerializeField] private Color backgroundColor = new Color(0.12f, 0.13f, 0.18f, 1f);
    [SerializeField] private int renderTextureSize = 256;

    [Header("Player Icon")]
    [SerializeField] private Color playerIconColor = new Color(1f, 0.92f, 0.016f, 1f);
    [SerializeField] private float playerIconSize = 8f;

    [Header("Markers")]
    [SerializeField] private MinimapMarker[] markers;

    private RenderTexture runtimeRT;
    private GameObject playerIcon;
    private readonly List<GameObject> markerIcons = new();

    [System.Serializable]
    public class MinimapMarker
    {
        public string label;
        public Transform target;
        public Color color = Color.white;
    }

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
    }

    void Start()
    {
        if (playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        SetupCamera();
        SetupPlayerIcon();
        SetupMarkers();
    }

    void OnDestroy()
    {
        if (Inst == this) Inst = null;
        if (runtimeRT != null)
        {
            runtimeRT.Release();
            Destroy(runtimeRT);
        }
    }

    void LateUpdate()
    {
        if (minimapCamera == null || playerTransform == null) return;

        Vector3 pos = playerTransform.position;
        minimapCamera.transform.position = new Vector3(pos.x, pos.y + cameraHeight, pos.z);

        UpdatePlayerIcon();
        UpdateMarkerPositions();
    }

    private void SetupCamera()
    {
        if (minimapCamera == null)
        {
            var camGO = new GameObject("MinimapCamera");
            camGO.transform.SetParent(transform);
            minimapCamera = camGO.AddComponent<Camera>();
        }

        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = orthoSize;
        minimapCamera.backgroundColor = backgroundColor;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        minimapCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("UI"));
        minimapCamera.depth = -10;

        RenderTexture target = renderTexture;
        if (target == null)
        {
            runtimeRT = new RenderTexture(renderTextureSize, renderTextureSize, 16);
            target = runtimeRT;
        }

        minimapCamera.targetTexture = target;
        if (minimapImage != null)
            minimapImage.texture = target;
    }

    private void SetupPlayerIcon()
    {
        if (minimapImage == null) return;
        playerIcon = CreateIconOnMinimap("PlayerIcon", playerIconColor, playerIconSize);
    }

    private void SetupMarkers()
    {
        if (minimapImage == null || markers == null) return;

        foreach (var m in markers)
        {
            if (m.target == null) continue;

            var icon = CreateIconOnMinimap($"Marker_{m.label}", m.color, 6f);

            if (!string.IsNullOrEmpty(m.label))
            {
                var labelGO = new GameObject($"Label_{m.label}");
                labelGO.transform.SetParent(icon.transform, false);
                var tmp = labelGO.AddComponent<TextMeshProUGUI>();
                tmp.text = m.label;
                tmp.fontSize = 8;
                tmp.color = m.color;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableWordWrapping = false;
                var lrt = labelGO.GetComponent<RectTransform>();
                lrt.anchoredPosition = new Vector2(0, -8f);
                lrt.sizeDelta = new Vector2(60, 12);
            }

            markerIcons.Add(icon);
        }
    }

    private GameObject CreateIconOnMinimap(string name, Color color, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(minimapImage.transform, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var iconRT = go.GetComponent<RectTransform>();
        iconRT.sizeDelta = new Vector2(size, size);
        return go;
    }

    private void UpdatePlayerIcon()
    {
        if (playerIcon == null || minimapImage == null) return;
        playerIcon.GetComponent<RectTransform>().anchoredPosition = WorldToMinimapPos(playerTransform.position);
    }

    private void UpdateMarkerPositions()
    {
        if (markers == null) return;

        int iconIdx = 0;
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i].target == null) continue;
            if (iconIdx >= markerIcons.Count) break;

            var mrt = markerIcons[iconIdx].GetComponent<RectTransform>();
            mrt.anchoredPosition = WorldToMinimapPos(markers[i].target.position);
            iconIdx++;
        }
    }

    private Vector2 WorldToMinimapPos(Vector3 worldPos)
    {
        if (minimapCamera == null || minimapImage == null) return Vector2.zero;

        Vector3 camPos = minimapCamera.transform.position;
        float relX = (worldPos.x - camPos.x) / (orthoSize * 2f);
        float relZ = (worldPos.z - camPos.z) / (orthoSize * 2f);

        Rect rect = minimapImage.rectTransform.rect;
        return new Vector2(relX * rect.width, relZ * rect.height);
    }
}
