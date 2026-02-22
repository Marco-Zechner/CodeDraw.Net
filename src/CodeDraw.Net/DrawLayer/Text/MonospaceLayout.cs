using System.Numerics;
using MarcoZechner.ColorDotNet;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer.Text;

public sealed class MonospaceLayout(GlyphCache glyphs, FontMetricsProvider metrics)
{

    public void GetCellMetrics(TextStyle style, out float cellW, out float lineH, out float baselineFromTop)
    {
        var sizePx = (int)MathF.Round(style.SizePx);
        if (sizePx <= 0) { cellW = lineH = baselineFromTop = 0; return; }

        var fm = GetCachedMetrics(style, sizePx);

        cellW = style.OverrideCellWidthPx ?? (fm.MonoAdvancePx + style.ExtraCellGapPx);

        var baseLineH = (fm.MaxAbovePx + fm.MaxBelowPx) + style.ExtraAbovePx + style.ExtraBelowPx;
        lineH = style.OverrideLineHeightPx ?? (baseLineH + style.ExtraLineGapPx);

        baselineFromTop = style.ExtraAbovePx + fm.MaxAbovePx;
    }

    public TextMetrics Measure(string text, TextStyle style)
    {
        if (string.IsNullOrEmpty(text)) return new TextMetrics(0, 0);

        GetCellMetrics(style, out var cellW, out var lineH, out _);

        int cols = 0, maxCols = 0;
        var lines = 1;

        foreach (var c in text.Where(c => c != '\r'))
        {
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

        var sizePx = (int)MathF.Round(style.SizePx);
        if (sizePx <= 0) return;

        GetCellMetrics(style, out var cellW, out var lineH, out var baselineFromTop);

        // Precompute line lengths in cells (needed for per-line alignment)
        var lineCols = new List<int>(32);
        {
            var cols = 0;
            foreach (var c in text.Where(c => c != '\r'))
            {
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

        var maxCols = lineCols.Prepend(0).Max();

        var totalW = maxCols * cellW;
        var totalH = lineCols.Count * lineH;

        // Global anchor adjustment (Top/Middle/Bottom uses totalH)
        var ax = style.Align switch
        {
            TextAlign.Left => 0,
            TextAlign.Center => totalW * 0.5f,
            TextAlign.Right => totalW,
            _ => 0
        };

        var ay = style.VAlign switch
        {
            TextVAlign.Top => 0,
            TextVAlign.Middle => totalH * 0.5f,
            TextVAlign.Bottom => totalH,
            _ => 0
        };

        var originX = x - ax;
        var originY = y - ay;

        var col = 0;
        var row = 0;

        foreach (var c in text)
        {
            switch (c)
            {
                case '\r': continue;
                case '\n':
                    col = 0;
                    row++;
                    continue;
            }

            var lineOff = LineOffsetPx(row);

            var cellX = originX + lineOff + col * cellW;
            var cellY = originY + row * lineH;

            var baselineY = cellY + baselineFromTop;

            if ((style.DebugMode & TextDebugMode.Cells) != 0)
                outDebugRects.Add(new DebugRect(cellX, cellY, cellW, lineH, new ColorF(0, 1, 0, 0.15f)));

            if ((style.DebugMode & TextDebugMode.Baseline) != 0)
                outDebugRects.Add(new DebugRect(cellX, baselineY, cellW, 1, new ColorF(1, 0, 0, 0.35f)));

            var gi = glyphs.GetGlyph(style.Font, sizePx, c);

            var gx = cellX + gi.BearingX;
            var gy = baselineY - gi.BearingY;

            if ((style.DebugMode & TextDebugMode.GlyphBoxes) != 0 && gi is { BitmapW: > 0, BitmapH: > 0 })
                outDebugRects.Add(new DebugRect(gx, gy, gi.BitmapW, gi.BitmapH, new ColorF(0, 0.6f, 1, 0.18f)));

            if (gi is { AtlasPage: >= 0, BitmapW: > 0, BitmapH: > 0 })
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

        return;

        // Per-line horizontal offset (to center/right each line individually)
        float LineOffsetPx(int r)
        {
            var lc = (r >= 0 && r < lineCols.Count) ? lineCols[r] : 0;
            var diff = maxCols - lc;

            if (style.Align == TextAlign.Left) return 0;

            if (style.MonospaceSnapLineAlignToCells)
            {
                // snap by whole cells (no half-cell wobble)
                var offCols = style.Align == TextAlign.Center ? (diff / 2) : diff;
                return offCols * cellW;
            }

            // fractional alignment
            var off = style.Align == TextAlign.Center ? (diff * 0.5f) : diff;
            return off * cellW;
        }
    }

    private FontMetrics GetCachedMetrics(TextStyle style, int sizePx)
    {
        var key = $"{Path.GetFullPath(style.Font.Path)}|{sizePx}|w={style.Font.Variant.Weight}|s={style.Font.Variant.Slant}";
        if (style.CachedKey != null && string.Equals(style.CachedKey, key, StringComparison.OrdinalIgnoreCase))
            return style.CachedMetrics;

        var fm = metrics.Get(style.Font, sizePx);
        style.CachedKey = key;
        style.CachedMetrics = fm;
        return fm;
    }

    public struct GlyphInstance
    {
        public float X, Y, W, H;
        public Vector4 Uv;
        public int Page;
        public ColorF Color;
    }

    public readonly record struct DebugRect(float X, float Y, float W, float H, ColorF Color);
}
