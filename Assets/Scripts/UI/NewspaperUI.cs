using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Game.API;

public class NewspaperUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject newspaperPanel;
    [SerializeField] private RawImage newspaperImage;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Button closeButton;

    [Header("Settings")]
    [SerializeField] private float zoomSpeed = 0.1f;
    [SerializeField] private float minZoom = 0.5f;
    [SerializeField] private float maxZoom = 2f;

    private Texture2D currentTexture;
    private RectTransform imageRect;
    private float currentZoom = 1f;
    private bool isLoading;
    private string loadedDate;

    void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (newspaperImage != null)
            imageRect = newspaperImage.GetComponent<RectTransform>();

        if (newspaperPanel != null)
            newspaperPanel.SetActive(false);
    }

    void Update()
    {
        if (newspaperPanel == null || !newspaperPanel.activeSelf) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            currentZoom = Mathf.Clamp(currentZoom + scroll * zoomSpeed * 10f, minZoom, maxZoom);
            ApplyZoom();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
            Hide();
    }

    public async void Show(string date = null)
    {
        if (newspaperPanel == null) return;

        newspaperPanel.SetActive(true);

        if (date == loadedDate && currentTexture != null)
        {
            ApplyTexture();
            return;
        }

        await LoadNewspaper(date);
    }

    public void Hide()
    {
        if (newspaperPanel != null)
            newspaperPanel.SetActive(false);

        if (PlayerStateController.Inst != null)
            PlayerStateController.Inst.SetState(PlayerState.MOVING);
    }

    private async Task LoadNewspaper(string date)
    {
        if (isLoading) return;
        isLoading = true;

        try
        {
            var tex = await NewspaperAPI.GetNewspaperImage(date);
            if (tex == null)
            {
                Debug.LogWarning("[NewspaperUI] Failed to load newspaper image");
                return;
            }

            if (currentTexture != null)
                Destroy(currentTexture);

            currentTexture = tex;
            loadedDate = date;
            currentZoom = 1f;
            ApplyTexture();
        }
        finally
        {
            isLoading = false;
        }
    }

    private void ApplyTexture()
    {
        if (newspaperImage == null || currentTexture == null) return;

        newspaperImage.texture = currentTexture;
        ApplyZoom();

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            scrollRect.horizontalNormalizedPosition = 0.5f;
        }
    }

    private void ApplyZoom()
    {
        if (imageRect == null || currentTexture == null) return;

        float w = currentTexture.width * currentZoom;
        float h = currentTexture.height * currentZoom;
        imageRect.sizeDelta = new Vector2(w, h);
    }

    void OnDestroy()
    {
        if (currentTexture != null)
            Destroy(currentTexture);
    }
}
