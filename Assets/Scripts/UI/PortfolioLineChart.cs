using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Game.API.DTO;
using System.Collections.Generic;

[RequireComponent(typeof(CanvasRenderer))]
public class PortfolioLineChart : MaskableGraphic, IPointerMoveHandler, IPointerExitHandler
{
    [Header("Line")]
    [SerializeField] private Color lineColor = new Color(0.4f, 0.7f, 1f);
    [SerializeField] private Color gainColor = new Color(0.15f, 0.75f, 0.35f);
    [SerializeField] private Color lossColor = new Color(0.85f, 0.22f, 0.22f);
    [SerializeField] private float lineWidth = 2f;

    [Header("Baseline")]
    [SerializeField] private Color baselineColor = new Color(1f, 1f, 1f, 0.2f);
    [SerializeField] private float baselineWidth = 1f;

    [Header("Grid")]
    [SerializeField] private bool drawGrid = true;
    [SerializeField] private int gridRows = 4;
    [SerializeField] private Color gridColor = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private float gridLineWidth = 0.5f;

    [Header("Axes")]
    [SerializeField] private float leftMargin = 65f;
    [SerializeField] private float rightMargin = 10f;
    [SerializeField] private float topMargin = 40f;
    [SerializeField] private float bottomMargin = 28f;
    [SerializeField] private Color axisLineColor = new Color(1f, 1f, 1f, 0.2f);
    [SerializeField] private float axisLineWidth = 1f;
    [SerializeField] private float tickLength = 4f;
    [SerializeField] private Color labelColor = new Color(1f, 1f, 1f, 0.7f);
    [SerializeField] private float labelFontSize = 13f;
    [SerializeField] private TMP_FontAsset labelFont;

    private NetWorthPointDTO[] data;
    private double startingCash = 10000;
    private readonly List<TMP_Text> priceLabelPool = new List<TMP_Text>();
    private readonly List<TMP_Text> dateLabelPool = new List<TMP_Text>();
    private int activePriceLabels;
    private int activeDateLabels;

    private float minValue;
    private float maxValue;

    private GameObject tooltipGO;
    private TMP_Text tooltipText;
    private Image tooltipBg;
    private RectTransform tooltipRT;

    public void SetData(NetWorthPointDTO[] history, double startCash = 10000)
    {
        data = history;
        startingCash = startCash;
        ComputeRange();
        SetVerticesDirty();
        UpdateLabels();
    }

    public void Clear()
    {
        data = null;
        SetVerticesDirty();
        HideAllLabels();
        HideTooltip();
    }

    private void ComputeRange()
    {
        minValue = float.MaxValue;
        maxValue = float.MinValue;

        if (data == null || data.Length == 0) return;

        foreach (var p in data)
        {
            float nw = (float)p.net_worth;
            if (nw < minValue) minValue = nw;
            if (nw > maxValue) maxValue = nw;
        }

        float baseline = (float)startingCash;
        if (baseline < minValue) minValue = baseline;
        if (baseline > maxValue) maxValue = baseline;

        float range = maxValue - minValue;
        if (range < 1f) range = 100f;
        float pad = range * 0.1f;
        minValue -= pad;
        maxValue += pad;
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

        if (data == null || data.Length < 2) return;

        GetChartArea(out float cx0, out float cy0, out float cw, out float ch);
        float range = maxValue - minValue;
        if (range < 0.01f || cw <= 0 || ch <= 0) return;

        // Axis lines
        float halfAxis = axisLineWidth * 0.5f;
        AddQuad(vh,
            new Vector2(cx0 - halfAxis, cy0),
            new Vector2(cx0 + halfAxis, cy0 + ch), axisLineColor);
        AddQuad(vh,
            new Vector2(cx0, cy0 - halfAxis),
            new Vector2(cx0 + cw, cy0 + halfAxis), axisLineColor);

        // Grid
        if (drawGrid)
        {
            for (int i = 1; i < gridRows; i++)
            {
                float t = (float)i / gridRows;
                float y = cy0 + t * ch;
                AddQuad(vh,
                    new Vector2(cx0, y - gridLineWidth * 0.5f),
                    new Vector2(cx0 + cw, y + gridLineWidth * 0.5f), gridColor);
            }
        }

        // Baseline (starting cash)
        float baselineY = cy0 + (((float)startingCash - minValue) / range) * ch;
        AddQuad(vh,
            new Vector2(cx0, baselineY - baselineWidth * 0.5f),
            new Vector2(cx0 + cw, baselineY + baselineWidth * 0.5f), baselineColor);

        // Line segments
        int count = data.Length;
        float step = cw / (count - 1);
        float halfLine = lineWidth * 0.5f;

        for (int i = 0; i < count - 1; i++)
        {
            float x0 = cx0 + i * step;
            float x1 = cx0 + (i + 1) * step;
            float y0 = cy0 + (((float)data[i].net_worth - minValue) / range) * ch;
            float y1 = cy0 + (((float)data[i + 1].net_worth - minValue) / range) * ch;

            bool above = data[i + 1].net_worth >= startingCash;
            Color segColor = above ? gainColor : lossColor;

            DrawLineSegment(vh, new Vector2(x0, y0), new Vector2(x1, y1), halfLine, segColor);
        }
    }

    private void DrawLineSegment(VertexHelper vh, Vector2 a, Vector2 b, float halfWidth, Color c)
    {
        Vector2 dir = (b - a).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x) * halfWidth;

        int idx = vh.currentVertCount;
        vh.AddVert(new Vector3(a.x + perp.x, a.y + perp.y), c, Vector2.zero);
        vh.AddVert(new Vector3(b.x + perp.x, b.y + perp.y), c, Vector2.zero);
        vh.AddVert(new Vector3(b.x - perp.x, b.y - perp.y), c, Vector2.zero);
        vh.AddVert(new Vector3(a.x - perp.x, a.y - perp.y), c, Vector2.zero);
        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }

    // ── Tooltip ──

    public void OnPointerMove(PointerEventData eventData)
    {
        if (data == null || data.Length < 2) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPos);

        GetChartArea(out float cx0, out float cy0, out float cw, out float ch);
        if (cw <= 0) return;

        float relX = localPos.x - cx0;
        if (relX < 0 || relX > cw) { HideTooltip(); return; }

        float step = cw / (data.Length - 1);
        int idx = Mathf.Clamp(Mathf.RoundToInt(relX / step), 0, data.Length - 1);

        var point = data[idx];
        double nw = point.net_worth;
        double change = nw - startingCash;
        double changePct = startingCash > 0 ? (change / startingCash) * 100 : 0;
        string sign = change >= 0 ? "+" : "";
        string changeColor = change >= 0 ? "#26BF59" : "#D93838";
        string dateStr = FormatDate(point.date);

        EnsureTooltip();
        tooltipText.text = $"<b>${nw:N2}</b>\n<size=11>{dateStr}  <color={changeColor}>{sign}${change:N2} ({sign}{changePct:F1}%)</color></size>";
        tooltipText.ForceMeshUpdate();

        float tooltipW = Mathf.Max(140f, tooltipText.preferredWidth + 16f);
        float tooltipH = tooltipText.preferredHeight + 10f;
        tooltipRT.sizeDelta = new Vector2(tooltipW, tooltipH);

        float pointX = cx0 + idx * step;
        float pointY = cy0 + (((float)nw - minValue) / (maxValue - minValue)) * ch;

        float tipX = pointX;
        float tipY = pointY + 12f;

        Rect rect = GetPixelAdjustedRect();
        if (tipX + tooltipW * 0.5f > rect.xMax - 5f)
            tipX = rect.xMax - 5f - tooltipW * 0.5f;
        if (tipX - tooltipW * 0.5f < rect.xMin + 5f)
            tipX = rect.xMin + 5f + tooltipW * 0.5f;
        if (tipY + tooltipH > rect.yMax - 5f)
            tipY = pointY - tooltipH - 4f;

        tooltipRT.anchoredPosition = new Vector2(tipX, tipY);
        tooltipGO.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }

    private void EnsureTooltip()
    {
        if (tooltipGO != null) return;

        tooltipGO = new GameObject("ChartTooltip", typeof(RectTransform));
        tooltipGO.transform.SetParent(transform, false);
        tooltipRT = tooltipGO.GetComponent<RectTransform>();
        tooltipRT.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRT.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRT.pivot = new Vector2(0.5f, 0f);
        tooltipRT.sizeDelta = new Vector2(150f, 40f);

        tooltipBg = tooltipGO.AddComponent<Image>();
        tooltipBg.color = new Color(0.08f, 0.08f, 0.12f, 0.94f);
        tooltipBg.raycastTarget = false;

        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(tooltipGO.transform, false);
        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(8f, 4f);
        textRT.offsetMax = new Vector2(-8f, -4f);

        tooltipText = textGO.AddComponent<TextMeshProUGUI>();
        tooltipText.fontSize = 13f;
        tooltipText.color = Color.white;
        tooltipText.alignment = TextAlignmentOptions.Center;
        tooltipText.enableWordWrapping = false;
        tooltipText.overflowMode = TextOverflowModes.Overflow;
        tooltipText.raycastTarget = false;
        if (labelFont != null) tooltipText.font = labelFont;

        tooltipGO.SetActive(false);
    }

    private void HideTooltip()
    {
        if (tooltipGO != null)
            tooltipGO.SetActive(false);
    }

    // ── Labels ──

    private void UpdateLabels()
    {
        if (data == null || data.Length < 2) { HideAllLabels(); return; }

        GetChartArea(out float cx0, out float cy0, out float cw, out float ch);
        float range = maxValue - minValue;
        if (range < 0.01f || cw <= 0 || ch <= 0) return;

        int priceCount = gridRows + 1;
        EnsurePool(priceLabelPool, priceCount);
        activePriceLabels = priceCount;

        for (int i = 0; i <= gridRows; i++)
        {
            float t = (float)i / gridRows;
            float val = minValue + t * range;
            float y = cy0 + t * ch;

            var label = priceLabelPool[i];
            label.gameObject.SetActive(true);
            label.text = $"${val:N0}";
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.rectTransform.anchoredPosition = new Vector2(cx0 - tickLength - 3f, y);
        }

        int count = data.Length;
        float step = cw / (count - 1);
        int dateInterval = Mathf.Max(1, count / 5);
        int dateCount = 0;
        for (int i = 0; i < count; i += dateInterval) dateCount++;

        EnsurePool(dateLabelPool, dateCount);
        activeDateLabels = dateCount;

        int labelIdx = 0;
        for (int i = 0; i < count; i += dateInterval)
        {
            float cx = cx0 + i * step;
            var label = dateLabelPool[labelIdx];
            label.gameObject.SetActive(true);
            label.text = FormatDate(data[i].date);
            label.alignment = TextAlignmentOptions.Top;
            label.rectTransform.anchoredPosition = new Vector2(cx, cy0 - tickLength - 2f);
            labelIdx++;
        }

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
        rt.sizeDelta = new Vector2(60f, 16f);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = labelFontSize;
        tmp.color = labelColor;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        if (labelFont != null) tmp.font = labelFont;

        return tmp;
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
