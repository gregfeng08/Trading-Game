using UnityEngine;
using UnityEngine.UI;
using Game.API.DTO;

/// <summary>
/// Custom UI Graphic that draws OHLC candlestick bars.
/// Add to a GameObject with a RectTransform — candles fill the rect automatically.
/// </summary>
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

    private PriceRowDTO[] data;

    public float MinPrice { get; private set; }
    public float MaxPrice { get; private set; }

    public void SetData(PriceRowDTO[] priceData)
    {
        data = priceData;
        ComputeRange();
        SetVerticesDirty();
    }

    public void Clear()
    {
        data = null;
        SetVerticesDirty();
    }

    private void ComputeRange()
    {
        MinPrice = float.MaxValue;
        MaxPrice = float.MinValue;

        if (data == null) return;
        foreach (var p in data)
        {
            if (p.low_price < MinPrice) MinPrice = p.low_price;
            if (p.high_price > MaxPrice) MaxPrice = p.high_price;
        }

        float range = MaxPrice - MinPrice;
        if (range < 0.01f) range = 1f;
        float pad = range * 0.06f;
        MinPrice -= pad;
        MaxPrice += pad;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (data == null || data.Length == 0) return;

        Rect rect = GetPixelAdjustedRect();
        float w = rect.width;
        float h = rect.height;
        float x0 = rect.xMin;
        float y0 = rect.yMin;
        float range = MaxPrice - MinPrice;
        if (range < 0.01f) return;

        // Grid lines
        if (drawGrid)
        {
            for (int i = 1; i < gridRows; i++)
            {
                float t = (float)i / gridRows;
                float y = y0 + t * h;
                AddQuad(vh, new Vector2(x0, y - gridLineWidth * 0.5f),
                            new Vector2(x0 + w, y + gridLineWidth * 0.5f), gridColor);
            }
        }

        int count = data.Length;
        float slotW = w / count;
        float bodyW = slotW * bodyWidthRatio;
        float halfBody = bodyW * 0.5f;
        float halfWick = wickWidth * 0.5f;

        for (int i = 0; i < count; i++)
        {
            var c = data[i];
            float cx = x0 + (i + 0.5f) * slotW;

            float yOpen  = y0 + ((c.open_price  - MinPrice) / range) * h;
            float yClose = y0 + ((c.close_price - MinPrice) / range) * h;
            float yHigh  = y0 + ((c.high_price  - MinPrice) / range) * h;
            float yLow   = y0 + ((c.low_price   - MinPrice) / range) * h;

            bool bull = c.close_price >= c.open_price;
            Color col = bull ? bullColor : bearColor;

            // Wick
            AddQuad(vh, new Vector2(cx - halfWick, yLow),
                        new Vector2(cx + halfWick, yHigh), col);

            // Body
            float bTop = Mathf.Max(yOpen, yClose);
            float bBot = Mathf.Min(yOpen, yClose);
            if (bTop - bBot < 1f) bTop = bBot + 1f;
            AddQuad(vh, new Vector2(cx - halfBody, bBot),
                        new Vector2(cx + halfBody, bTop), col);
        }
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
