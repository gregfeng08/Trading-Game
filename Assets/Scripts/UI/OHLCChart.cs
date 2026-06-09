using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Game.API.DTO;
using System.Collections.Generic;

[RequireComponent(typeof(CanvasRenderer))]
public class OHLCChart : MaskableGraphic, IPointerMoveHandler, IPointerExitHandler
{
    [Header("Candle Colors")]
    [SerializeField] private Color bullColor = new Color(0.15f, 0.75f, 0.35f);
    [SerializeField] private Color bearColor = new Color(0.85f, 0.22f, 0.22f);

    [Header("Line Mode")]
    [SerializeField] private Color lineColor = new Color(0.5f, 0.72f, 1f);
    [SerializeField] private float lineWidth = 2f;

    [Header("Sizing")]
    [SerializeField] private float bodyWidthRatio = 0.65f;
    [SerializeField] private float wickWidth = 1.5f;

    [Header("Grid")]
    [SerializeField] private bool drawGrid = true;
    [SerializeField] private int gridRows = 4;
    [SerializeField] private Color gridColor = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private float gridLineWidth = 0.5f;

    [Header("Axes")]
    [SerializeField] private float leftMargin = 60f;
    [SerializeField] private float rightMargin = 10f;
    [SerializeField] private float topMargin = 40f;
    [SerializeField] private float bottomMargin = 28f;
    [SerializeField] private Color axisLineColor = new Color(1f, 1f, 1f, 0.2f);
    [SerializeField] private float axisLineWidth = 1f;
    [SerializeField] private float tickLength = 4f;
    [SerializeField] private Color labelColor = new Color(1f, 1f, 1f, 0.7f);
    [SerializeField] private float labelFontSize = 13f;
    [SerializeField] private int dateTickInterval = 20;
    [SerializeField] private TMP_FontAsset labelFont;

    private PriceRowDTO[] data;
    private readonly List<TMP_Text> priceLabelPool = new List<TMP_Text>();
    private readonly List<TMP_Text> dateLabelPool = new List<TMP_Text>();
    private int activePriceLabels;
    private int activeDateLabels;

    public float MinPrice { get; private set; }
    public float MaxPrice { get; private set; }

    private bool _showCandlesticks = true;
    public bool ShowCandlesticks
    {
        get => _showCandlesticks;
        set
        {
            if (_showCandlesticks == value) return;
            _showCandlesticks = value;
            ComputeRange();
            SetVerticesDirty();
            UpdateLabels();
        }
    }

    private bool _showWicks = true;
    public bool ShowWicks
    {
        get => _showWicks;
        set
        {
            if (_showWicks == value) return;
            _showWicks = value;
            ComputeRange();
            SetVerticesDirty();
            UpdateLabels();
        }
    }

    public void SetData(PriceRowDTO[] priceData)
    {
        data = priceData;
        ComputeRange();
        SetVerticesDirty();
        UpdateLabels();
    }

    public void Clear()
    {
        data = null;
        SetVerticesDirty();
        HideAllLabels();
    }

    private void ComputeRange()
    {
        MinPrice = float.MaxValue;
        MaxPrice = float.MinValue;

        if (data == null) return;
        foreach (var p in data)
        {
            if (!_showCandlesticks)
            {
                if ((float)p.close_price < MinPrice) MinPrice = (float)p.close_price;
                if ((float)p.close_price > MaxPrice) MaxPrice = (float)p.close_price;
            }
            else if (_showWicks)
            {
                if ((float)p.low_price < MinPrice) MinPrice = (float)p.low_price;
                if ((float)p.high_price > MaxPrice) MaxPrice = (float)p.high_price;
            }
            else
            {
                float lo = Mathf.Min((float)p.open_price, (float)p.close_price);
                float hi = Mathf.Max((float)p.open_price, (float)p.close_price);
                if (lo < MinPrice) MinPrice = lo;
                if (hi > MaxPrice) MaxPrice = hi;
            }
        }

        float range = MaxPrice - MinPrice;
        if (range < 0.01f) range = 1f;
        float pad = range * 0.06f;
        MinPrice -= pad;
        MaxPrice += pad;
    }

    private void GetChartArea(out float cx0, out float cy0, out float cw, out float ch)
    {
        Rect rect = GetPixelAdjustedRect();
        cx0 = rect.xMin + leftMargin;
        cy0 = rect.yMin + bottomMargin;
        cw = rect.width - leftMargin - rightMargin;
        ch = rect.height - bottomMargin - topMargin;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (data == null || data.Length == 0) return;

        GetChartArea(out float cx0, out float cy0, out float cw, out float ch);
        float range = MaxPrice - MinPrice;
        if (range < 0.01f || cw <= 0 || ch <= 0) return;

        Rect rect = GetPixelAdjustedRect();

        // Axis lines (L-shape along left and bottom of chart area)
        float halfAxis = axisLineWidth * 0.5f;
        AddQuad(vh,
            new Vector2(cx0 - halfAxis, cy0),
            new Vector2(cx0 + halfAxis, cy0 + ch), axisLineColor);
        AddQuad(vh,
            new Vector2(cx0, cy0 - halfAxis),
            new Vector2(cx0 + cw, cy0 + halfAxis), axisLineColor);

        // Grid lines + price tick marks
        if (drawGrid)
        {
            for (int i = 0; i <= gridRows; i++)
            {
                float t = (float)i / gridRows;
                float y = cy0 + t * ch;

                if (i > 0 && i < gridRows)
                {
                    AddQuad(vh,
                        new Vector2(cx0, y - gridLineWidth * 0.5f),
                        new Vector2(cx0 + cw, y + gridLineWidth * 0.5f), gridColor);
                }

                AddQuad(vh,
                    new Vector2(cx0 - tickLength, y - halfAxis),
                    new Vector2(cx0, y + halfAxis), axisLineColor);
            }
        }

        // Data rendering
        int count = data.Length;
        float slotW = cw / count;

        if (!_showCandlesticks)
        {
            // Line chart mode — connected close prices
            float halfLine = lineWidth * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float y = cy0 + (((float)data[i].close_price - MinPrice) / range) * ch;
                float x = cx0 + (i + 0.5f) * slotW;

                if (i > 0)
                {
                    float prevY = cy0 + (((float)data[i - 1].close_price - MinPrice) / range) * ch;
                    float prevX = cx0 + (i - 0.5f) * slotW;
                    DrawLineSegment(vh, prevX, prevY, x, y, halfLine, lineColor);
                }

                AddQuad(vh, new Vector2(x - 2.5f, y - 2.5f),
                            new Vector2(x + 2.5f, y + 2.5f), lineColor);
            }
        }
        else
        {
            // Candlestick mode
            float bodyW = slotW * bodyWidthRatio;
            float halfBody = bodyW * 0.5f;
            float halfWick = wickWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var c = data[i];
                float cx = cx0 + (i + 0.5f) * slotW;

                float yOpen  = cy0 + (((float)c.open_price  - MinPrice) / range) * ch;
                float yClose = cy0 + (((float)c.close_price - MinPrice) / range) * ch;
                float yHigh  = cy0 + (((float)c.high_price  - MinPrice) / range) * ch;
                float yLow   = cy0 + (((float)c.low_price   - MinPrice) / range) * ch;

                bool bull = c.close_price >= c.open_price;
                Color col = bull ? bullColor : bearColor;

                if (_showWicks)
                {
                    AddQuad(vh, new Vector2(cx - halfWick, yLow),
                                new Vector2(cx + halfWick, yHigh), col);
                }

                float bTop = Mathf.Max(yOpen, yClose);
                float bBot = Mathf.Min(yOpen, yClose);
                if (bTop - bBot < 1f) bTop = bBot + 1f;
                AddQuad(vh, new Vector2(cx - halfBody, bBot),
                            new Vector2(cx + halfBody, bTop), col);
            }
        }

        // Date tick marks — match the label spacing
        float minSpacing = 55f;
        int tickInterval = Mathf.Max(dateTickInterval, Mathf.CeilToInt(minSpacing / slotW));
        for (int i = 0; i < count; i += tickInterval)
        {
            float cx = cx0 + (i + 0.5f) * slotW;
            AddQuad(vh,
                new Vector2(cx - halfAxis, cy0 - tickLength),
                new Vector2(cx + halfAxis, cy0), axisLineColor);
        }
    }

    // ── Label Management ──

    private void UpdateLabels()
    {
        if (data == null || data.Length == 0) { HideAllLabels(); return; }

        GetChartArea(out float cx0, out float cy0, out float cw, out float ch);
        float range = MaxPrice - MinPrice;
        if (range < 0.01f || cw <= 0 || ch <= 0) return;

        // Price labels along left axis
        int priceCount = gridRows + 1;
        EnsurePool(priceLabelPool, priceCount);
        activePriceLabels = priceCount;

        for (int i = 0; i <= gridRows; i++)
        {
            float t = (float)i / gridRows;
            float price = MinPrice + t * range;
            float y = cy0 + t * ch;

            var label = priceLabelPool[i];
            label.gameObject.SetActive(true);
            label.text = FormatPrice(price);
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.rectTransform.anchoredPosition = new Vector2(cx0 - tickLength - 3f, y);
        }

        // Date labels along bottom axis — auto-space to avoid overlap
        int count = data.Length;
        float slotW = cw / count;
        float minDateSpacing = 55f;
        int effectiveInterval = Mathf.Max(dateTickInterval, Mathf.CeilToInt(minDateSpacing / slotW));
        int dateCount = 0;
        for (int i = 0; i < count; i += effectiveInterval) dateCount++;

        EnsurePool(dateLabelPool, dateCount);
        activeDateLabels = dateCount;

        int labelIdx = 0;
        for (int i = 0; i < count; i += effectiveInterval)
        {
            float cx = cx0 + (i + 0.5f) * slotW;

            var label = dateLabelPool[labelIdx];
            label.gameObject.SetActive(true);
            label.text = FormatDate(data[i].date);
            label.alignment = TextAlignmentOptions.Top;
            label.rectTransform.anchoredPosition = new Vector2(cx, cy0 - tickLength - 2f);
            labelIdx++;
        }

        // Hide unused labels
        for (int i = activePriceLabels; i < priceLabelPool.Count; i++)
            priceLabelPool[i].gameObject.SetActive(false);
        for (int i = activeDateLabels; i < dateLabelPool.Count; i++)
            dateLabelPool[i].gameObject.SetActive(false);
    }

    private void HideAllLabels()
    {
        foreach (var l in priceLabelPool) if (l != null) l.gameObject.SetActive(false);
        foreach (var l in dateLabelPool) if (l != null) l.gameObject.SetActive(false);
        activePriceLabels = 0;
        activeDateLabels = 0;
    }

    private void EnsurePool(List<TMP_Text> pool, int needed)
    {
        while (pool.Count < needed)
            pool.Add(CreateLabel());
    }

    private TMP_Text CreateLabel()
    {
        var go = new GameObject("AxisLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(50f, 16f);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = labelFontSize;
        tmp.color = labelColor;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        if (labelFont != null) tmp.font = labelFont;

        return tmp;
    }

    private string FormatPrice(float price)
    {
        float abs = Mathf.Abs(price);
        if (abs < 1f) return $"${price:F4}";
        if (abs >= 1000f) return $"${price:F0}";
        return $"${price:F2}";
    }

    private string FormatDate(string isoDate)
    {
        if (System.DateTime.TryParse(isoDate, out var dt))
            return dt.ToString("M/dd");
        return isoDate;
    }

    private void DrawLineSegment(VertexHelper vh, float x1, float y1, float x2, float y2, float halfWidth, Color c)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float len = Mathf.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f) return;

        float nx = -dy / len * halfWidth;
        float ny = dx / len * halfWidth;

        int idx = vh.currentVertCount;
        vh.AddVert(new Vector3(x1 + nx, y1 + ny), c, Vector2.zero);
        vh.AddVert(new Vector3(x2 + nx, y2 + ny), c, Vector2.up);
        vh.AddVert(new Vector3(x2 - nx, y2 - ny), c, Vector2.one);
        vh.AddVert(new Vector3(x1 - nx, y1 - ny), c, Vector2.right);
        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }

    private void AddQuad(VertexHelper vh, Vector2 bl, Vector2 tr, Color c)
    {
        int idx = vh.currentVertCount;
        vh.AddVert(new Vector3(bl.x, bl.y), c, Vector2.zero);
        vh.AddVert(new Vector3(bl.x, tr.y), c, Vector2.up);
        vh.AddVert(new Vector3(tr.x, tr.y), c, Vector2.one);
        vh.AddVert(new Vector3(tr.x, bl.y), c, Vector2.right);
        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }

    // ── Hover Tooltip ──

    private GameObject tooltipGO;
    private TMP_Text tooltipText;
    private Image tooltipBg;
    private Image crosshairLine;

    public void OnPointerMove(PointerEventData eventData)
    {
        if (data == null || data.Length == 0) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            return;

        GetChartArea(out float cx0, out float cy0, out float cw, out float ch);
        if (cw <= 0) return;

        float slotW = cw / data.Length;
        int index = Mathf.FloorToInt((localPoint.x - cx0) / slotW);
        if (index < 0 || index >= data.Length)
        {
            HideTooltip();
            return;
        }

        EnsureTooltip();

        var c = data[index];
        string date = FormatDate(c.date);

        if (!_showCandlesticks)
        {
            tooltipText.text = $"{date}\n{FormatPrice((float)c.close_price)}";
        }
        else
        {
            bool isFlat = c.open_price == c.high_price && c.open_price == c.low_price && c.open_price == c.close_price;
            tooltipText.text = isFlat
                ? $"{date}\n{FormatPrice((float)c.close_price)}"
                : $"{date}\nO: {FormatPrice((float)c.open_price)}  H: {FormatPrice((float)c.high_price)}\nL: {FormatPrice((float)c.low_price)}  C: {FormatPrice((float)c.close_price)}";
        }

        // Vertical crosshair line at the candle center
        float candleCenterX = cx0 + (index + 0.5f) * slotW;
        var lineRT = crosshairLine.rectTransform;
        lineRT.anchoredPosition = new Vector2(candleCenterX, cy0);
        lineRT.sizeDelta = new Vector2(1f, ch);
        crosshairLine.gameObject.SetActive(true);

        // Position tooltip near mouse but clamped to chart
        float tooltipW = tooltipText.preferredWidth + 16f;
        float tooltipH = tooltipText.preferredHeight + 10f;
        var bgRT = tooltipBg.rectTransform;
        bgRT.sizeDelta = new Vector2(tooltipW, tooltipH);

        float tx = localPoint.x + 15f;
        float ty = localPoint.y + 15f;
        Rect rect = GetPixelAdjustedRect();
        tx = Mathf.Clamp(tx, rect.xMin, rect.xMax - tooltipW);
        ty = Mathf.Clamp(ty, rect.yMin, rect.yMax - tooltipH);
        bgRT.anchoredPosition = new Vector2(tx, ty);

        tooltipGO.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }

    private void HideTooltip()
    {
        if (tooltipGO != null)
            tooltipGO.SetActive(false);
        if (crosshairLine != null)
            crosshairLine.gameObject.SetActive(false);
    }

    private void EnsureTooltip()
    {
        if (tooltipGO != null) return;

        // Vertical crosshair line (dashed via a semi-transparent thin image)
        var lineGO = new GameObject("CrosshairLine");
        lineGO.transform.SetParent(transform, false);
        var clRT = lineGO.AddComponent<RectTransform>();
        Vector2 pp = rectTransform.pivot;
        clRT.anchorMin = pp;
        clRT.anchorMax = pp;
        clRT.pivot = new Vector2(0.5f, 0f);
        crosshairLine = lineGO.AddComponent<Image>();
        crosshairLine.color = new Color(1f, 1f, 1f, 0.3f);
        crosshairLine.raycastTarget = false;
        lineGO.SetActive(false);

        tooltipGO = new GameObject("ChartTooltip");
        tooltipGO.transform.SetParent(transform, false);

        var rt = tooltipGO.AddComponent<RectTransform>();
        // Use parent's pivot as anchor so localPoint coordinates align directly
        Vector2 parentPivot = rectTransform.pivot;
        rt.anchorMin = parentPivot;
        rt.anchorMax = parentPivot;
        rt.pivot = new Vector2(0f, 0f);

        tooltipBg = tooltipGO.AddComponent<Image>();
        tooltipBg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
        tooltipBg.raycastTarget = false;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(tooltipGO.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(8, 5);
        textRT.offsetMax = new Vector2(-8, -5);

        tooltipText = textGO.AddComponent<TextMeshProUGUI>();
        tooltipText.fontSize = 12f;
        tooltipText.color = new Color(0.9f, 0.9f, 0.95f);
        tooltipText.alignment = TextAlignmentOptions.TopLeft;
        tooltipText.enableWordWrapping = false;
        tooltipText.overflowMode = TextOverflowModes.Overflow;
        tooltipText.raycastTarget = false;
        if (labelFont != null) tooltipText.font = labelFont;

        tooltipGO.SetActive(false);
    }
}
