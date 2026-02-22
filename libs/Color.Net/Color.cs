using System.Runtime.CompilerServices;

namespace MarcoZechner.ColorDotNet;

public readonly partial record struct Color
{
    private readonly byte _r;
    private readonly byte _g;
    private readonly byte _b;
    private readonly byte _a;

    // ---- ctors ----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Color(byte r, byte g, byte b, byte a = 255)
    {
        _r = r; _g = g; _b = b; _a = a;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Color(byte grayscale, byte alpha = 255)
        : this(grayscale, grayscale, grayscale, alpha) { }

    // Interprets rgba as 0xRRGGBBAA (canonical packed layout)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Color(uint rgba)
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

    public static Color FromHex(string hex, HexType type = HexType.RRGGBBAA)
        => new(ParseHexToRgba(hex, type));

    public static Color FromHsv(int hue, byte saturation, byte value, byte alpha = 255)
    {
        var (r, g, b) = HsvToRgb(hue, saturation, value);
        return new Color(r, g, b, alpha);
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
    public static Color Lerp(in Color a, in Color b, float t)
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

        return new Color(Clamp255(rf), Clamp255(gf), Clamp255(bf), Clamp255(af));
    }

    // -----------------------------------------------------------------------------------------
    // Conversions for your API style.
    // -----------------------------------------------------------------------------------------

    /// <summary>Enables: Method(Color.BLACK) where BLACK is a uint const; or new Color(Color.TRANSPARENT).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Color(uint rgba) => new(rgba);

    /// <summary>Convenient pack back to uint if you want it.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint(Color c) => c.ToRgba32();

    /// <summary>Implicit conversion to float-color.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorF(Color c) => new(
        c._r * (1f / 255f),
        c._g * (1f / 255f),
        c._b * (1f / 255f),
        c._a * (1f / 255f)
    );

    #region Constants

    public const uint TRANSPARENT = 0x00000000u;
    public const uint ALICE_BLUE = 0xF0F8FFFFu;
    public const uint ANTIQUE_WHITE = 0xFAEBD7FFu;
    public const uint AQUA = 0x00FFFFFFu;
    public const uint AQUAMARINE = 0x7FFFD4FFu;
    public const uint AZURE = 0xF0FFFFFFu;
    public const uint BEIGE = 0xF5F5DCFFu;
    public const uint BISQUE = 0xFFE4C4FFu;
    public const uint BLACK = 0x000000FFu;
    public const uint BLANCHED_ALMOND = 0xFFEBCDFFu;
    public const uint BLUE = 0x0000FFFFu;
    public const uint BLUE_VIOLET = 0x8A2BE2FFu;
    public const uint BROWN = 0xA52A2AFFu;
    public const uint BURLY_WOOD = 0xDEB887FFu;
    public const uint CADET_BLUE = 0x5F9EA0FFu;
    public const uint CHARTREUSE = 0x7FFF00FFu;
    public const uint CHOCOLATE = 0xD2691EFFu;
    public const uint CORAL = 0xFF7F50FFu;
    public const uint CORNFLOWER_BLUE = 0x6495EDFFu;
    public const uint CORNSILK = 0xFFF8DCFFu;
    public const uint CRIMSON = 0xDC143CFFu;
    public const uint CYAN = 0x00FFFFFFu;
    public const uint DARK_BLUE = 0x00008BFFu;
    public const uint DARK_CYAN = 0x008B8BFFu;
    public const uint DARK_GOLDEN_ROD = 0xB8860BFFu;

    /// <summary>A medium gray color (50% brightness).</summary>
    public const uint GRAY = 0x808080FFu;

    /// <summary>A dark gray color (25% brightness).</summary>
    public const uint DARK_GRAY = 0x404040FFu;

    public const uint DARK_GREEN = 0x006400FFu;
    public const uint DARK_KHAKI = 0xBDB76BFFu;
    public const uint DARK_MAGENTA = 0x8B008BFFu;
    public const uint DARK_OLIVE_GREEN = 0x556B2FFFu;
    public const uint DARK_ORANGE = 0xFF8C00FFu;
    public const uint DARK_ORCHID = 0x9932CCFFu;
    public const uint DARK_RED = 0x8B0000FFu;
    public const uint DARK_SALMON = 0xE9967AFFu;
    public const uint DARK_SEA_GREEN = 0x8FBC8FFFu;
    public const uint DARK_SLATE_BLUE = 0x483D8BFFu;
    public const uint DARK_SLATE_GRAY = 0x2F4F4FFFu;
    public const uint DARK_TURQUOISE = 0x00CED1FFu;
    public const uint DARK_VIOLET = 0x9400D3FFu;
    public const uint DEEP_PINK = 0xFF1493FFu;
    public const uint DEEP_SKY_BLUE = 0x00BFFFFFu;
    public const uint DIM_GRAY = 0x696969FFu;
    public const uint DODGER_BLUE = 0x1E90FFFFu;
    public const uint FIRE_BRICK = 0xB22222FFu;
    public const uint FLORAL_WHITE = 0xFFFAF0FFu;
    public const uint FOREST_GREEN = 0x228B22FFu;
    public const uint FUCHSIA = 0xFF00FFFFu;
    public const uint GAINSBORO = 0xDCDCDCFFu;
    public const uint GHOST_WHITE = 0xF8F8FFFFu;
    public const uint GOLD = 0xFFD700FFu;
    public const uint GOLDEN_ROD = 0xDAA520FFu;
    public const uint GREEN = 0x008000FFu;
    public const uint GREEN_YELLOW = 0xADFF2FFFu;
    public const uint HONEY_DEW = 0xF0FFF0FFu;
    public const uint HOT_PINK = 0xFF69B4FFu;
    public const uint INDIAN_RED = 0xCD5C5CFFu;
    public const uint INDIGO = 0x4B0082FFu;
    public const uint IVORY = 0xFFFFF0FFu;
    public const uint KHAKI = 0xF0E68CFFu;
    public const uint LAVENDER = 0xE6E6FAFFu;
    public const uint LAVENDER_BLUSH = 0xFFF0F5FFu;
    public const uint LAWN_GREEN = 0x7CFC00FFu;
    public const uint LEMON_CHIFFON = 0xFFFACDFFu;
    public const uint LIGHT_BLUE = 0xADD8E6FFu;
    public const uint LIGHT_CORAL = 0xF08080FFu;
    public const uint LIGHT_CYAN = 0xE0FFFFFFu;
    public const uint LIGHT_GOLDEN_ROD_YELLOW = 0xFAFAD2FFu;

    /// <summary>A light gray color (75% brightness).</summary>
    public const uint LIGHT_GRAY = 0xD3D3D3FFu;

    public const uint LIGHT_GREEN = 0x90EE90FFu;
    public const uint LIGHT_PINK = 0xFFB6C1FFu;
    public const uint LIGHT_SALMON = 0xFFA07AFFu;
    public const uint LIGHT_SEA_GREEN = 0x20B2AAFFu;
    public const uint LIGHT_SKY_BLUE = 0x87CEFAFFu;
    public const uint LIGHT_SLATE_GRAY = 0x778899FFu;
    public const uint LIGHT_STEEL_BLUE = 0xB0C4DEFFu;
    public const uint LIGHT_YELLOW = 0xFFFFE0FFu;
    public const uint LIME = 0x00FF00FFu;
    public const uint LIME_GREEN = 0x32CD32FFu;
    public const uint LINEN = 0xFAF0E6FFu;
    public const uint MAGENTA = 0xFF00FFFFu;
    public const uint MAROON = 0x800000FFu;
    public const uint MEDIUM_AQUA_MARINE = 0x66CDAAFFu;
    public const uint MEDIUM_BLUE = 0x0000CDFFu;
    public const uint MEDIUM_ORCHID = 0xBA55D3FFu;
    public const uint MEDIUM_PURPLE = 0x9370DBFFu;
    public const uint MEDIUM_SEA_GREEN = 0x3CB371FFu;
    public const uint MEDIUM_SLATE_BLUE = 0x7B68EEFFu;
    public const uint MEDIUM_SPRING_GREEN = 0x00FA9AFFu;
    public const uint MEDIUM_TURQUOISE = 0x48D1CCFFu;
    public const uint MEDIUM_VIOLET_RED = 0xC71585FFu;
    public const uint MIDNIGHT_BLUE = 0x191970FFu;
    public const uint MINT_CREAM = 0xF5FFFAFFu;
    public const uint MISTY_ROSE = 0xFFE4E1FFu;
    public const uint MOCCASIN = 0xFFE4B5FFu;
    public const uint NAVAJO_WHITE = 0xFFDEADFFu;
    public const uint NAVY = 0x000080FFu;
    public const uint OLD_LACE = 0xFDF5E6FFu;
    public const uint OLIVE = 0x808000FFu;
    public const uint OLIVE_DRAB = 0x6B8E23FFu;
    public const uint ORANGE = 0xFFA500FFu;
    public const uint ORANGE_RED = 0xFF4500FFu;
    public const uint ORCHID = 0xDA70D6FFu;
    public const uint PALE_GOLDEN_ROD = 0xEEE8AAFFu;
    public const uint PALE_GREEN = 0x98FB98FFu;
    public const uint PALE_TURQUOISE = 0xAFEEEEFFu;
    public const uint PALE_VIOLET_RED = 0xDB7093FFu;
    public const uint PAPAYA_WHIP = 0xFFEFD5FFu;
    public const uint PEACH_PUFF = 0xFFDAB9FFu;
    public const uint PERU = 0xCD853FFFu;
    public const uint PINK = 0xFFC0CBFFu;
    public const uint PLUM = 0xDDA0DDFFu;
    public const uint POWDER_BLUE = 0xB0E0E6FFu;
    public const uint PURPLE = 0x800080FFu;
    public const uint RED = 0xFF0000FFu;
    public const uint ROSY_BROWN = 0xBC8F8FFFu;
    public const uint ROYAL_BLUE = 0x4169E1FFu;
    public const uint SADDLE_BROWN = 0x8B4513FFu;
    public const uint SALMON = 0xFA8072FFu;
    public const uint SANDY_BROWN = 0xF4A460FFu;
    public const uint SEA_GREEN = 0x2E8B57FFu;
    public const uint SEA_SHELL = 0xFFF5EEFFu;
    public const uint SIENNA = 0xA0522DFFu;
    public const uint SILVER = 0xC0C0C0FFu;
    public const uint SKY_BLUE = 0x87CEEBFFu;
    public const uint SLATE_BLUE = 0x6A5ACDFFu;
    public const uint SLATE_GRAY = 0x708090FFu;
    public const uint SNOW = 0xFFFAFAFFu;
    public const uint SPRING_GREEN = 0x00FF7FFFu;
    public const uint STEEL_BLUE = 0x4682B4FFu;
    public const uint TAN = 0xD2B48CFFu;
    public const uint TEAL = 0x008080FFu;
    public const uint THISTLE = 0xD8BFD8FFu;
    public const uint TOMATO = 0xFF6347FFu;
    public const uint TURQUOISE = 0x40E0D0FFu;
    public const uint VIOLET = 0xEE82EEFFu;
    public const uint WHEAT = 0xF5DEB3FFu;
    public const uint WHITE = 0xFFFFFFFFu;
    public const uint WHITE_SMOKE = 0xF5F5F5FFu;
    public const uint YELLOW = 0xFFFF00FFu;
    public const uint YELLOW_GREEN = 0x9ACD32FFu;

    #endregion
}