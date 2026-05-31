using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.API.DTO;
using System.Collections.Generic;

[RequireComponent(typeof(CanvasRenderer))]
public class OHLCChart : MaskableGraphic
{
    [Header("Candle Colors")]
    [SerializeField] private Color bullColor = new Color(0.15f, 0.75f, 0.35f);
    [SerializeField] private Color bearColor = new Color(0.85f, 0.22f, 0.22f);

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
            if (_showWicks)
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

        // Candles
        int count = data.Length;
        float slotW = cw / count;
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
}
