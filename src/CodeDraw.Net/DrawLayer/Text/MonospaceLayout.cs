using System.Numerics;
using MarcoZechner.ColorDotNet;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer.Text;

public sealed class MonospaceLayout
{
    private readonly GlyphCache _glyphs;
    private readonly FontMetricsProvider _metrics;

    public MonospaceLayout(GlyphCache glyphs, FontMetricsProvider metrics)
    {
        _glyphs = glyphs;
        _metrics = metrics;
    }

    public void GetCellMetrics(TextStyle style, out float cellW, out float lineH, out float baselineFromTop)
    {
        int sizePx = (int)MathF.Round(style.SizePx);
        if (sizePx <= 0) { cellW = lineH = baselineFromTop = 0; return; }

        var fm = GetCachedMetrics(style, sizePx);

        cellW = style.OverrideCellWidthPx ?? (fm.MonoAdvancePx + style.ExtraCellGapPx);

        float baseLineH = (fm.MaxAbovePx + fm.MaxBelowPx) + style.ExtraAbovePx + style.ExtraBelowPx;
        lineH = style.OverrideLineHeightPx ?? (baseLineH + style.ExtraLineGapPx);

        baselineFromTop = style.ExtraAbovePx + fm.MaxAbovePx;
    }

    public TextMetrics Measure(string text, TextStyle style)
    {
        if (string.IsNullOrEmpty(text)) return new TextMetrics(0, 0);

        GetCellMetrics(style, out float cellW, out float lineH, out _);

        int cols = 0, maxCols = 0;
        int lines = 1;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r') continue;

            if (c == '\n')
            {
                if (cols > maxCols) maxCols = cols;
                cols = 0;
                lines++;
                continue;
            }

            cols++;
        }

        if (cols > maxCols) maxCols = cols;

        return new TextMetrics(maxCols * cellW, lines * lineH);
    }

    public void Layout(
        string text,
        float x,
        float y,
        TextStyle style,
        List<GlyphInstance> outGlyphs,
        List<DebugRect> outDebugRects)
    {
        outGlyphs.Clear();
        outDebugRects.Clear();

        if (string.IsNullOrEmpty(text)) return;

        int sizePx = (int)MathF.Round(style.SizePx);
        if (sizePx <= 0) return;

        GetCellMetrics(style, out float cellW, out float lineH, out float baselineFromTop);

        // Precompute line lengths in cells (needed for per-line alignment)
        var lineCols = new List<int>(32);
        {
            int cols = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\r') continue;
                if (c == '\n')
                {
                    lineCols.Add(cols);
                    cols = 0;
                    continue;
                }
                cols++;
            }
            lineCols.Add(cols);
        }

        int maxCols = 0;
        for (int i = 0; i < lineCols.Count; i++)
            if (lineCols[i] > maxCols) maxCols = lineCols[i];

        float totalW = maxCols * cellW;
        float totalH = lineCols.Count * lineH;

        // Global anchor adjustment (Top/Middle/Bottom uses totalH)
        float ax = style.Align switch
        {
            TextAlign.Left => 0,
            TextAlign.Center => totalW * 0.5f,
            TextAlign.Right => totalW,
            _ => 0
        };

        float ay = style.VAlign switch
        {
            TextVAlign.Top => 0,
            TextVAlign.Middle => totalH * 0.5f,
            TextVAlign.Bottom => totalH,
            _ => 0
        };

        float originX = x - ax;
        float originY = y - ay;

        int col = 0;
        int row = 0;

        // Per-line horizontal offset (to center/right each line individually)
        float LineOffsetPx(int r)
        {
            int lc = (r >= 0 && r < lineCols.Count) ? lineCols[r] : 0;
            int diff = maxCols - lc;

            if (style.Align == TextAlign.Left) return 0;

            if (style.MonospaceSnapLineAlignToCells)
            {
                // snap by whole cells (no half-cell wobble)
                int offCols = style.Align == TextAlign.Center ? (diff / 2) : diff;
                return offCols * cellW;
            }
            else
            {
                // fractional alignment
                float off = style.Align == TextAlign.Center ? (diff * 0.5f) : diff;
                return off * cellW;
            }
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r') continue;

            if (c == '\n')
            {
                col = 0;
                row++;
                continue;
            }

            float lineOff = LineOffsetPx(row);

            float cellX = originX + lineOff + col * cellW;
            float cellY = originY + row * lineH;

            float baselineY = cellY + baselineFromTop;

            if ((style.DebugMode & TextDebugMode.Cells) != 0)
                outDebugRects.Add(new DebugRect(cellX, cellY, cellW, lineH, new Color(0, 1, 0, 0.15f)));

            if ((style.DebugMode & TextDebugMode.Baseline) != 0)
                outDebugRects.Add(new DebugRect(cellX, baselineY, cellW, 1, new Color(1, 0, 0, 0.35f)));

            var gi = _glyphs.GetGlyph(style.Font, sizePx, c);

            float gx = cellX + gi.BearingX;
            float gy = baselineY - gi.BearingY;

            if ((style.DebugMode & TextDebugMode.GlyphBoxes) != 0 && gi.BitmapW > 0 && gi.BitmapH > 0)
                outDebugRects.Add(new DebugRect(gx, gy, gi.BitmapW, gi.BitmapH, new Color(0, 0.6f, 1, 0.18f)));

            if (gi.AtlasPage >= 0 && gi.BitmapW > 0 && gi.BitmapH > 0)
            {
                outGlyphs.Add(new GlyphInstance
                {
                    X = gx,
                    Y = gy,
                    W = gi.BitmapW,
                    H = gi.BitmapH,
                    Uv = gi.Uv,
                    Page = gi.AtlasPage,
                    Color = style.Color
                });
            }

            col++;
        }
    }

    private FontMetrics GetCachedMetrics(TextStyle style, int sizePx)
    {
        var key = $"{Path.GetFullPath(style.Font.Path)}|{sizePx}|w={style.Font.Variant.Weight}|s={style.Font.Variant.Slant}";
        if (style.CachedKey != null && string.Equals(style.CachedKey, key, StringComparison.OrdinalIgnoreCase))
            return style.CachedMetrics;

        var fm = _metrics.Get(style.Font, sizePx);
        style.CachedKey = key;
        style.CachedMetrics = fm;
        return fm;
    }

    public struct GlyphInstance
    {
        public float X, Y, W, H;
        public Vector4 Uv;
        public int Page;
        public Color Color;
    }

    public readonly record struct DebugRect(float X, float Y, float W, float H, Color Color);
}
