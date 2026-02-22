using System.Runtime.CompilerServices;

namespace MarcoZechner.ColorDotNet.RGB;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly partial record struct ColorF(float R, float G, float B, float A = 1f)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorF(float grayscale, float alpha = 1f) : this(grayscale, grayscale, grayscale, alpha) { }

    /// <summary>
    /// Supports "0xRGB", "0xRGBA", "0xARGB", "0xRRGGBB", "0xRRGGBBAA", "0xAARRGGBB"
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorF(uint rgba, ColorLayout layout = ColorLayout.RRGGBBAA) : this(new ColorB(rgba, layout)) { }
    
    private ColorF(ColorB b) : this(
        b.R / 255f,
        b.G / 255f,
        b.B / 255f,
        b.A / 255f)
    { }
    
    public ColorF((float r, float g, float b, float a) c) : this(c.r, c.g, c.b, c.a) { }

    /// <summary>
    /// Supports "#RGB", "#RGBA", "#ARGB", "#RRGGBB", "#RRGGBBAA", "#AARRGGBB"
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorF(string hex, ColorLayout layout = ColorLayout.RRGGBBAA) : this(new ColorB(hex, layout)) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ToRgba32(ColorLayout layout = ColorLayout.RRGGBBAA) => ((ColorB)this).ToRgba32(layout);

    public override string ToString() => $"ColorF(R:{R}, G:{G}, B:{B}, A:{A})";
    public string ToString(ColorLayout layout = ColorLayout.RRGGBBAA) => ((ColorB)this).ToString(layout);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ColorF Lerp(in ColorF a, in ColorF b, float t) => new(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t,
            a.A + (b.A - a.A) * t
        );
}