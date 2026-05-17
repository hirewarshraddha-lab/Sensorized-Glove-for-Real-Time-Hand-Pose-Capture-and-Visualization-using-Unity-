using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FingerGraphOverlay : MonoBehaviour
{
    [Header("Panel")]
    public RawImage graphImage;

    [Header("Source")]
    public IndexFingerSerial fingerSerial;

    [Header("Graph Size")]
    public int graphWidth = 900;
    public int graphHeight = 280;

    // Y range is NO LONGER set manually here.
    // It is read automatically from fingerSerial.adcMin / adcMax
    // after calibration sets them. A small padding is added so
    // the lines never touch the top or bottom edge.
    [Header("Y Axis Padding (ADC units added above and below range)")]
    public float yPadding = 10f;

    [Header("Colours")]
    public Color rawColor = new Color(0.92f, 0.30f, 0.30f, 1f);
    public Color filteredColor = new Color(0.35f, 0.68f, 1.00f, 1f);
    public Color bgColor = new Color(0.06f, 0.06f, 0.09f, 1f);
    public Color gridColor = new Color(0.18f, 0.20f, 0.26f, 1f);
    public Color axisColor = new Color(0.38f, 0.40f, 0.48f, 1f);

    [Header("Line Thickness")]
    public int rawThick = 1;
    public int filtThick = 2;

    private Texture2D _tex;
    private Queue<float> _rawQ = new Queue<float>();
    private Queue<float> _filtQ = new Queue<float>();

    // These are set each frame from fingerSerial calibration values
    private float _yMin;
    private float _yMax;

    void Start()
    {
        _tex = new Texture2D(graphWidth, graphHeight, TextureFormat.RGBA32, false);
        _tex.filterMode = FilterMode.Point;
        if (graphImage != null)
            graphImage.texture = _tex;
    }

    void Update()
    {
        if (fingerSerial == null) return;

        // ── Read Y range from calibration values every frame ──────────
        // This means as soon as calibration finishes and sets adcMin/adcMax,
        // the graph automatically adjusts its scale.
        _yMin = fingerSerial.adcMin - yPadding;
        _yMax = fingerSerial.adcMax + yPadding;

        // ── Raw ADC value ─────────────────────────────────────────────
        float rawADC = fingerSerial.rawADC;

        // ── Filtered angle converted back to ADC space ────────────────
        // filteredAngle is 0–angleMax degrees
        // We map it back to ADC range so both lines share the same Y axis
        float adcSpan = fingerSerial.adcMax - fingerSerial.adcMin;
        float filtADC = (fingerSerial.filteredAngle / fingerSerial.angleMax)
                        * adcSpan + fingerSerial.adcMin;

        _rawQ.Enqueue(rawADC);
        _filtQ.Enqueue(filtADC);

        while (_rawQ.Count > graphWidth) _rawQ.Dequeue();
        while (_filtQ.Count > graphWidth) _filtQ.Dequeue();

        Redraw();
    }

    void Redraw()
    {
        // Background
        Color[] bg = new Color[graphWidth * graphHeight];
        for (int i = 0; i < bg.Length; i++)
            bg[i] = bgColor;
        _tex.SetPixels(bg);

        // Grid — 5 horizontal lines
        for (int g = 0; g <= 5; g++)
        {
            int gy = Mathf.RoundToInt(g * (graphHeight - 1f) / 5f);
            HLine(gy, gridColor);
        }

        // Border
        HLine(0, axisColor);
        HLine(graphHeight - 1, axisColor);
        VLine(0, axisColor);
        VLine(graphWidth - 1, axisColor);

        // Raw line (red, thin, behind)
        PlotLine(_rawQ.ToArray(), rawColor, rawThick);
        // Filtered line (blue, thick, on top)
        PlotLine(_filtQ.ToArray(), filteredColor, filtThick);

        _tex.Apply();
    }

    void PlotLine(float[] arr, Color c, int thickness)
    {
        for (int x = 1; x < arr.Length; x++)
        {
            int y0 = ToPixelY(arr[x - 1]);
            int y1 = ToPixelY(arr[x]);
            DrawBresenham(x - 1, y0, x, y1, c, thickness);
        }
    }

    int ToPixelY(float value)
    {
        float t = Mathf.InverseLerp(_yMin, _yMax, value);
        return Mathf.Clamp(Mathf.RoundToInt(t * (graphHeight - 1)), 0, graphHeight - 1);
    }

    void DrawBresenham(int x0, int y0, int x1, int y1, Color c, int t)
    {
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = (dx > dy ? dx : -dy) / 2;
        while (true)
        {
            PaintPixel(x0, y0, c, t);
            if (x0 == x1 && y0 == y1) break;
            int e2 = err;
            if (e2 > -dx) { err -= dy; x0 += sx; }
            if (e2 < dy) { err += dx; y0 += sy; }
        }
    }

    void PaintPixel(int cx, int cy, Color c, int t)
    {
        for (int tx = -(t - 1); tx < t; tx++)
            for (int ty = -(t - 1); ty < t; ty++)
            {
                int px = cx + tx;
                int py = cy + ty;
                if (px >= 0 && px < graphWidth && py >= 0 && py < graphHeight)
                    _tex.SetPixel(px, py, c);
            }
    }

    void HLine(int y, Color c) { for (int x = 0; x < graphWidth; x++) _tex.SetPixel(x, y, c); }
    void VLine(int x, Color c) { for (int y = 0; y < graphHeight; y++) _tex.SetPixel(x, y, c); }

    void OnDestroy() { if (_tex != null) Destroy(_tex); }
}
