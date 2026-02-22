using System.Runtime.CompilerServices;

namespace MarcoZechner.ColorDotNet.RGB;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly partial record struct ColorB(byte R, byte G, byte B, byte A = 255)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorB(byte grayscale, byte alpha = 255) : this(grayscale, grayscale, grayscale, alpha) { }

    /// <summary>
    /// Supports "0xRGB", "0xRGBA", "0xARGB", "0xRRGGBB", "0xRRGGBBAA", "0xAARRGGBB"
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorB(uint packed, ColorLayout layout = ColorLayout.RRGGBBAA) : this(ColorCodec.UnpackToRgba32(packed, layout)) { }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorB((byte r, byte g, byte b, byte a) c) : this(c.r, c.g, c.b, c.a) { }

    /// <summary>
    /// Supports "#RGB", "#RGBA", "#ARGB", "#RRGGBB", "#RRGGBBAA", "#AARRGGBB"
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorB(string hex, ColorLayout layout = ColorLayout.RRGGBBAA) : this(ParseHexToRgba(hex, layout)) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ToRgba32(ColorLayout layout = ColorLayout.RRGGBBAA) => ColorCodec.ToPackedUInt(R, G, B, A, layout);
    
    public override string ToString() => $"ColorB(R:{R}, G:{G}, B:{B}, A:{A})";
    public string ToString(ColorLayout layout) => ParseRgbaToHex(ToRgba32(), layout);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ColorB Lerp(in ColorB a, in ColorB b, float t) => ColorF.Lerp(a, b, t);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ParseHexToRgba(string hex, ColorLayout layout = ColorLayout.RRGGBBAA)
        => ColorCodec.ParseHexToRgba(hex, layout);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ParseRgbaToHex(uint rgba, ColorLayout layout = ColorLayout.RRGGBBAA)
        => ColorCodec.ParseRgbaToHex(rgba, layout);
}