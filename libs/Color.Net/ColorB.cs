using System.Runtime.CompilerServices;

namespace MarcoZechner.ColorDotNet;

public readonly partial record struct ColorB
{
    private readonly byte _r;
    private readonly byte _g;
    private readonly byte _b;
    private readonly byte _a;

    // ---- ctors ----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorB(byte r, byte g, byte b, byte a = 255)
    {
        _r = r; _g = g; _b = b; _a = a;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorB(byte grayscale, byte alpha = 255)
        : this(grayscale, grayscale, grayscale, alpha) { }

    // Interprets rgba as 0xRRGGBBAA (canonical packed layout)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorB(uint rgba)
        : this(
            (byte)(rgba >> 24 & 0xFF),
            (byte)(rgba >> 16 & 0xFF),
            (byte)(rgba >>  8 & 0xFF),
            (byte)( rgba        & 0xFF)
        )
    { }

    // ---- byte views (get + init) ----

    public byte R { get => _r; init => _r = value; }
    public byte G { get => _g; init => _g = value; }
    public byte B { get => _b; init => _b = value; }
    public byte A { get => _a; init => _a = value; }

    // ---- HSV views (get + init) ----
    // Semantics:
    // - Setting Hue/Saturation/Value preserves the other two HSV components derived from current RGB,
    //   and preserves Alpha as-is.
    // - HSV is derived from current RGB, repeated edits can accumulate rounding noise (byte domain).

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

    public byte Saturation
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

    public byte Value
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

    /// <summary>Returns packed RGBA in canonical layout 0xRRGGBBAA.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ToRgba32()
        => (uint)_r << 24 | (uint)_g << 16 | (uint)_b << 8 | _a;

    // ---- factories ----

    public static ColorB FromHex(string hex, HexType type = HexType.RRGGBBAA)
        => new(ParseHexToRgba(hex, type));

    public static ColorB FromHsv(int hue, byte saturation, byte value, byte alpha = 255)
    {
        var (r, g, b) = HsvToRgb(hue, saturation, value);
        return new ColorB(r, g, b, alpha);
    }

    // ---- helpers ----

    internal static uint ParseHexToRgba(string hex, HexType type = HexType.RRGGBBAA)
    {
        if (hex is null) throw new ArgumentNullException(nameof(hex));
        if (hex.Length < 7 || hex[0] != '#')
            throw new ArgumentException("Must start with '#'.", nameof(hex));

        var s = hex.AsSpan(1);

        if (s.Length == 6)
        {
            // #RRGGBB -> RRGGBBAA (AA = FF)
            var rrggbb = Convert.ToUInt32(s.ToString(), 16);
            return rrggbb << 8 | 0xFFu;
        }

        if (s.Length == 8)
        {
            var x = Convert.ToUInt32(s.ToString(), 16);

            return type switch
            {
                HexType.RRGGBBAA => x,                                        // already canonical
                HexType.AARRGGBB => (x & 0x00FFFFFFu) << 8 | x >> 24 & 0xFFu, // AARRGGBB -> RRGGBBAA
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown HexType.")
            };
        }

        throw new ArgumentException("Use '#RRGGBB', '#RRGGBBAA', or '#AARRGGBB'.", nameof(hex));
    }

    // RGB<->HSV (byte domain for S,V). Hue is int degrees [0,360).
    private static (int h, byte s, byte v) RgbToHsv(byte r, byte g, byte b)
    {
        float rf = r / 255f, gf = g / 255f, bf = b / 255f;
        var max = MathF.Max(rf, MathF.Max(gf, bf));
        var min = MathF.Min(rf, MathF.Min(gf, bf));
        var d = max - min;

        float h;
        if (d == 0f) h = 0f;
        else if (Math.Abs(max - rf) < float.Epsilon) h = 60f * ((gf - bf) / d % 6f);
        else if (Math.Abs(max - gf) < float.Epsilon) h = 60f * ((bf - rf) / d + 2f);
        else h = 60f * ((rf - gf) / d + 4f);

        if (h < 0f) h += 360f;

        var s = max == 0f ? 0f : d / max;
        var v = max;

        return ((int)(h + 0.5f),
                (byte)(s * 255f + 0.5f),
                (byte)(v * 255f + 0.5f));
    }

    private static (byte r, byte g, byte b) HsvToRgb(int hue, byte sat, byte val)
    {
        var h = (hue % 360 + 360) % 360 / 60f; // 0..6
        var s = sat / 255f;
        var v = val / 255f;

        var i = (int)MathF.Floor(h) % 6;
        var f = h - MathF.Floor(h);

        var p = v * (1f - s);
        var q = v * (1f - f * s);
        var t = v * (1f - (1f - f) * s);

        var (rf, gf, bf) = i switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };

        return (ToByte(rf), ToByte(gf), ToByte(bf));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ToByte(float x)
    {
        if (x <= 0f) return 0;
        if (x >= 1f) return 255;
        return (byte)(x * 255f + 0.5f);
    }

    public override string ToString()
        => $"Color(R: {_r}, G: {_g}, B: {_b}, A: {_a})";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ColorB Lerp(in ColorB a, in ColorB b, float t)
    {
        // Lerp in float space, then quantize back to bytes.
        var rf = a._r + (b._r - a._r) * t;
        var gf = a._g + (b._g - a._g) * t;
        var bf = a._b + (b._b - a._b) * t;
        var af = a._a + (b._a - a._a) * t;

        // rf/gf/bf/af are in [0..255] if t in [0..1], but clamp anyway.
        static byte Clamp255(float x)
        {
            if (x <= 0f) return 0;
            if (x >= 255f) return 255;
            return (byte)(x + 0.5f);
        }

        return new ColorB(Clamp255(rf), Clamp255(gf), Clamp255(bf), Clamp255(af));
    }

    // -----------------------------------------------------------------------------------------
    // Conversions for your API style.
    // -----------------------------------------------------------------------------------------

    /// <summary>Enables: Method(Color.BLACK) where BLACK is a uint const; or new Color(Color.TRANSPARENT).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorB(uint rgba) => new(rgba);

    /// <summary>Convenient pack back to uint if you want it.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint(ColorB c) => c.ToRgba32();

    /// <summary>Implicit conversion to float-color.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorF(ColorB c) => new(
        c._r * (1f / 255f),
        c._g * (1f / 255f),
        c._b * (1f / 255f),
        c._a * (1f / 255f)
    );
}