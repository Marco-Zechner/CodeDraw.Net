using System.Runtime.CompilerServices;
using MarcoZechner.ColorDotNet.RGB;

namespace MarcoZechner.ColorDotNet.CMYK;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly partial record struct ColorCmykF(float C, float M, float Y, float K, float A = 1f)
{
    public ColorCmykF(float grayscale, float alpha = 1f) : this(grayscale, grayscale, grayscale, grayscale, alpha) { }
    public ColorCmykF((float c, float m, float y, float k, float a) c) : this(c.c, c.m, c.y, c.k, c.a) { }
    
    public override string ToString() => $"ColorCmykF(C:{C}, M:{M}, Y:{Y}, K:{K}, A:{A})";

    // Core conversion: CMYK_F <-> RGB_F
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ColorCmykF FromRgb(ColorF rgb)
    {
        var (c, m, y, k) = RgbToCmyk(rgb.R, rgb.G, rgb.B);
        return new ColorCmykF(c, m, y, k, rgb.A);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorF ToRgb()
    {
        var (r, g, b) = CmykToRgb(C, M, Y, K);
        return new ColorF(r, g, b, A);
    }

    // ---------- math (float domain) ----------

    private static (float c, float m, float y, float k) RgbToCmyk(float r, float g, float b)
    {
        // expects r,g,b in [0..1] (no clamp to keep it "mathy")
        var max = MathF.Max(r, MathF.Max(g, b));
        var k = 1f - max;

        if (k >= 1f) // pure black
            return (0f, 0f, 0f, 1f);

        var inv = 1f - k; // == max
        var c = (1f - r - k) / inv;
        var m = (1f - g - k) / inv;
        var y = (1f - b - k) / inv;
        return (c, m, y, k);
    }

    private static (float r, float g, float b) CmykToRgb(float c, float m, float y, float k)
    {
        var r = (1f - c) * (1f - k);
        var g = (1f - m) * (1f - k);
        var b = (1f - y) * (1f - k);
        return (r, g, b);
    }
}