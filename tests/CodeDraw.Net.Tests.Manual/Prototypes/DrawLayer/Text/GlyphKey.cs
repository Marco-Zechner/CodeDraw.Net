namespace MarcoZechner.CodeDrawDotNet.Tests.Manual.Prototypes.DrawLayer.Text;

public readonly struct GlyphKey(string fontPath, int sizePx, uint glyphIndex)
    : IEquatable<GlyphKey>
{
    public readonly string FontPath = fontPath;
    public readonly int SizePx = sizePx;
    public readonly uint GlyphIndex = glyphIndex;

    public bool Equals(GlyphKey other) =>
        SizePx == other.SizePx &&
        GlyphIndex == other.GlyphIndex &&
        string.Equals(FontPath, other.FontPath, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) =>
        obj is GlyphKey g && Equals(g);

    public override int GetHashCode()
    {
        unchecked
        {
            var h = StringComparer.OrdinalIgnoreCase.GetHashCode(FontPath);
            h = (h * 397) ^ SizePx;
            h = (h * 397) ^ (int)GlyphIndex;
            return h;
        }
    }

    public static bool operator ==(GlyphKey left, GlyphKey right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(GlyphKey left, GlyphKey right)
    {
        return !(left == right);
    }
}