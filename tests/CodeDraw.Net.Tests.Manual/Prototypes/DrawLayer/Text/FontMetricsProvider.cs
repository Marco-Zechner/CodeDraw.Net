using SharpFont;

namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer.Text;

public sealed class FontMetricsProvider : IDisposable
{
    private readonly FontLibrary _lib = new();
    private readonly Dictionary<(string path, int sizePx), FontMetrics> _cache = new();

    private readonly GlyphCache _glyphs;

    public FontMetricsProvider(GlyphCache glyphs)
    {
        _glyphs = glyphs;
    }

    public void Dispose()
    {
        _lib.Dispose();
        _cache.Clear();
    }

    public FontMetrics Get(FontRef font, int sizePx)
    {
        var key = (Path.GetFullPath(font.Path), sizePx);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var face = new FontFace(_lib, key.Item1);
        face.SetPixelSize((uint)sizePx);

        var f = face.Face;

        // Face metrics are in font units; scale to px.
        float asc = 0, desc = 0, line = 0;
        if (f.UnitsPerEM > 0)
        {
            float scale = sizePx / (float)f.UnitsPerEM;
            asc = f.Ascender * scale;
            desc = -f.Descender * scale;
            line = f.Height * scale;
        }

        // Extents & monospace cell width from glyph samples
        float maxAbove = 0;
        float maxBelow = 0;
        float monoAdv = 0;

        // CapHeight and xHeight approximations via sample glyphs.
        float capHeight = 0;
        float xHeight = 0;

        foreach (char c in SampleSet())
        {
            var g = _glyphs.GetGlyph(font, sizePx, c);

            float above = g.BearingY;
            float below = g.BitmapH - g.BearingY;

            if (above > maxAbove) maxAbove = above;
            if (below > maxBelow) maxBelow = below;

            // Required cell advance to avoid overlap:
            // - include negative left bearing overhang (minX)
            // - ensure >= advance
            float left = g.BearingX;
            float right = g.BearingX + g.BitmapW;
            float minX = Math.Min(0f, left);
            float span = right - minX;

            float needed = Math.Max(g.AdvanceX, span);
            if (needed > monoAdv) monoAdv = needed;

            if (c == 'H') capHeight = Math.Max(capHeight, above); // rough, but stable
            if (c == 'x') xHeight = Math.Max(xHeight, above);     // rough, but stable
        }

        // Fallbacks if those weren’t present in the font.
        if (capHeight <= 0.01f) capHeight = maxAbove;
        if (xHeight <= 0.01f) xHeight = maxAbove * 0.7f;

        var m = new FontMetrics(
            sizePx,
            AscenderPx: asc,
            DescenderPx: desc,
            RecommendedLinePx: line,

            CapHeightPx: capHeight,
            XHeightPx: xHeight,

            MaxAbovePx: maxAbove,
            MaxBelowPx: maxBelow,

            MonoAdvancePx: MathF.Ceiling(monoAdv)
        );

        face.Dispose();

        _cache[key] = m;
        return m;
    }

    private static IEnumerable<char> SampleSet()
    {
        for (int i = 32; i <= 126; i++)
            yield return (char)i;

        // German + block
        yield return 'Ä';
        yield return 'Ö';
        yield return 'Ü';
        yield return 'ä';
        yield return 'ö';
        yield return 'ü';
        yield return 'ß';
        yield return '█';

        // Good width offenders
        yield return 'W';
        yield return 'M';
        yield return '0';
        yield return ' ';
        yield return 'x';
        yield return 'H';
        yield return 'g';
        yield return 'y';
        yield return 'p';
    }
}
