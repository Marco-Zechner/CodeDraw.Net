using System.Runtime.CompilerServices;
using MarcoZechner.ColorDotNet.RGB;

namespace MarcoZechner.ColorDotNet.HSV;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly partial record struct ColorHsvF(int H, float S, float V, float A = 1f)
{
    public override string ToString() => $"ColorHsvF(H:{H}, S:{S}, V:{V}, A:{A})";

    // Core conversion: HSV_F <-> RGB_F
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ColorHsvF FromRgb(ColorF rgb)
    {
        var (h, s, v) = RgbToHsv(rgb.R, rgb.G, rgb.B);
        return new ColorHsvF(h, s, v, rgb.A);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorF ToRgb()
    {
        var (r, g, b) = HsvToRgb(H, S, V);
        return new ColorF(r, g, b, A);
    }

    // ---------- math (float domain) ----------

    private static (int h, float s, float v) RgbToHsv(float r, float g, float b)
    {
        var max = MathF.Max(r, MathF.Max(g, b));
        var min = MathF.Min(r, MathF.Min(g, b));
        var d = max - min;

        float h;
        if (d == 0f) h = 0f;
        else if (Math.Abs(max - r) < float.Epsilon) h = 60f * (((g - b) / d) % 6f);
        else if (Math.Abs(max - g) < float.Epsilon) h = 60f * ((b - r) / d + 2f);
        else h = 60f * ((r - g) / d + 4f);

        if (h < 0f) h += 360f;

        var s = max == 0f ? 0f : d / max;
        var v = max;

        return ((int)(h + 0.5f), s, v);
    }

    private static (float r, float g, float b) HsvToRgb(int hue, float sat, float val)
    {
        var h = ((hue % 360) + 360) % 360 / 60f; // 0..6
        var s = sat;
        var v = val;

        var i = (int)MathF.Floor(h) % 6;
        var f = h - MathF.Floor(h);

        var p = v * (1f - s);
        var q = v * (1f - f * s);
        var t = v * (1f - (1f - f) * s);

        return i switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };
    }
}