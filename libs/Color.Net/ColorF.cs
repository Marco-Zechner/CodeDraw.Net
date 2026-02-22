using System.Runtime.CompilerServices;

namespace MarcoZechner.ColorDotNet;

public readonly partial record struct ColorF
{
    private readonly float _r;
    private readonly float _g;
    private readonly float _b;
    private readonly float _a;

    // ---- ctors ----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorF(float r, float g, float b, float a = 1f)
    {
        _r = r; _g = g; _b = b; _a = a;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorF(float grayscale, float alpha = 1f)
        : this(grayscale, grayscale, grayscale, alpha) { }

    /// <summary>Interprets rgba as 0xRRGGBBAA (canonical packed layout) and converts to floats in [0..1].</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorF(uint rgba)
        : this(
            (rgba >> 24 & 0xFF) * (1f / 255f),
            (rgba >> 16 & 0xFF) * (1f / 255f),
            (rgba >>  8 & 0xFF) * (1f / 255f),
            ( rgba        & 0xFF) * (1f / 255f)
        )
    { }

    // ---- float views (get + init) ----

    public float R { get => _r; init => _r = value; }
    public float G { get => _g; init => _g = value; }
    public float B { get => _b; init => _b = value; }
    public float A { get => _a; init => _a = value; }

    // ---- HSV views (get + init) ----
    // Same semantics as Color:
    // - Setting Hue/Saturation/Value preserves other HSV components derived from current RGB,
    //   and preserves Alpha as-is.
    // - Edits can accumulate numeric noise (float math).

    public int Hue
    {
        get
        {
            var (h, _, _) = RgbToHsv(_r, _g, _b);
            return h;
        }
        init
        {
            var (_, s, v) = RgbToHsv(_r, _g, _b);
            var (r, g, b) = HsvToRgb(value, s, v);
            _r = r; _g = g; _b = b;
        }
    }

    public float Saturation
    {
        get
        {
            var (_, s, _) = RgbToHsv(_r, _g, _b);
            return s;
        }
        init
        {
            var (h, _, v) = RgbToHsv(_r, _g, _b);
            var (r, g, b) = HsvToRgb(h, value, v);
            _r = r; _g = g; _b = b;
        }
    }

    public float Value
    {
        get
        {
            var (_, _, v) = RgbToHsv(_r, _g, _b);
            return v;
        }
        init
        {
            var (h, s, _) = RgbToHsv(_r, _g, _b);
            var (r, g, b) = HsvToRgb(h, s, value);
            _r = r; _g = g; _b = b;
        }
    }

    // ---- packed conversions ----

    /// <summary>
    /// Packs to canonical 0xRRGGBBAA.
    /// Packing clamps each channel to [0..1] and rounds to nearest byte.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ToRgba32()
    {
        static byte ToByteClamped(float x)
        {
            if (x <= 0f) return 0;
            if (x >= 1f) return 255;
            return (byte)(x * 255f + 0.5f);
        }

        var r = ToByteClamped(_r);
        var g = ToByteClamped(_g);
        var b = ToByteClamped(_b);
        var a = ToByteClamped(_a);

        return (uint)r << 24 | (uint)g << 16 | (uint)b << 8 | a;
    }

    // ---- factories ----

    public static ColorF FromHex(string hex, HexType type = HexType.RRGGBBAA)
        => new(ColorB.ParseHexToRgba(hex, type)); // reuse exact parsing logic

    public static ColorF FromHsv(int hue, float saturation, float value, float alpha = 1f)
    {
        var (r, g, b) = HsvToRgb(hue, saturation, value);
        return new ColorF(r, g, b, alpha);
    }

    public override string ToString()
        => $"ColorF(R: {_r}, G: {_g}, B: {_b}, A: {_a})";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ColorF Lerp(in ColorF a, in ColorF b, float t)
        => new(
            a._r + (b._r - a._r) * t,
            a._g + (b._g - a._g) * t,
            a._b + (b._b - a._b) * t,
            a._a + (b._a - a._a) * t
        );

    // -----------------------------------------------------------------------------------------
    // Conversions
    // -----------------------------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorF(uint rgba) => new(rgba);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint(ColorF c) => c.ToRgba32();

    /// <summary>Float -> byte quantization uses clamping to [0..1] + rounding to nearest byte.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorB(ColorF c)
    {
        static byte ToByteClamped(float x)
        {
            if (x <= 0f) return 0;
            if (x >= 1f) return 255;
            return (byte)(x * 255f + 0.5f);
        }

        return new ColorB(ToByteClamped(c._r), ToByteClamped(c._g), ToByteClamped(c._b), ToByteClamped(c._a));
    }

    // RGB<->HSV (float domain). Hue int degrees [0..360). s,v are floats (typically 0..1).
    private static (int h, float s, float v) RgbToHsv(float r, float g, float b)
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

    private static (float r, float g, float b) HsvToRgb(int hue, float sat, float val)
    {
        var h = (hue % 360 + 360) % 360 / 60f; // 0..6
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