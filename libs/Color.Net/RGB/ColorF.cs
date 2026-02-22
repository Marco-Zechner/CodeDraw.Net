using System.Runtime.CompilerServices;

namespace MarcoZechner.ColorDotNet.RGB;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly partial record struct ColorF(float R, float G, float B, float A = 1f)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorF(float grayscale, float alpha = 1f) : this(grayscale, grayscale, grayscale, alpha) { }

    /// <summary>
    /// From packed 0xRRGGBBAA
    /// </summary>
    /// <param name="rgba"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorF(uint rgba) : this(new RGB.ColorB(rgba)) { }
    
    private ColorF(RGB.ColorB b) : this(
        b.R / 255f,
        b.G / 255f,
        b.B / 255f,
        b.A / 255f)
    { }

    /// <summary>
    /// Supports "#RRGGBB", "#RRGGBBAA", "#AARRGGBB"
    /// </summary>
    /// <param name="hex"></param>
    /// <param name="type"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorF(string hex, HexType type = HexType.RRGGBBAA) : this(new RGB.ColorB(hex, type)) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ToRgba32() => ((ColorB)this).ToRgba32();

    public override string ToString() => $"ColorF(R:{R}, G:{G}, B:{B}, A:{A})";
    public string ToHex(HexType type = HexType.RRGGBBAA) => ((ColorB)this).ToString(type);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ColorF Lerp(in ColorF a, in ColorF b, float t) => new(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t,
            a.A + (b.A - a.A) * t
        );

    // ---- shared helpers (internal) ----

    internal static (float c, float m, float y, float k) RgbToCmyk(float r, float g, float b)
    {
        var max = MathF.Max(r, MathF.Max(g, b));
        var k = 1f - max;

        if (k >= 1f) // pure black
            return (0f, 0f, 0f, 1f);

        var inv = 1f - k;
        var c = (1f - r - k) / inv;
        var m = (1f - g - k) / inv;
        var y = (1f - b - k) / inv;
        return (c, m, y, k);
    }

    internal static (int h, float s, float v) RgbToHsv(float r, float g, float b)
    {
        var max = MathF.Max(r, MathF.Max(g, b));
        var min = MathF.Min(r, MathF.Min(g, b));
        var d = max - min;

        float h;
        if (d == 0f) h = 0f;
        else if (Math.Abs(max - r) < float.Epsilon) h = 60f * ((g - b) / d % 6f);
        else if (Math.Abs(max - g) < float.Epsilon) h = 60f * ((b - r) / d + 2f);
        else h = 60f * ((r - g) / d + 4f);

        if (h < 0f) h += 360f;

        var s = max == 0f ? 0f : d / max;
        var v = max;

        return ((int)(h + 0.5f), s, v);
    }
}