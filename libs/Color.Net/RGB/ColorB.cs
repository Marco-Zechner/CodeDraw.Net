using System.Runtime.CompilerServices;

namespace MarcoZechner.ColorDotNet.RGB;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly partial record struct ColorB(byte R, byte G, byte B, byte A = 255)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorB(byte grayscale, byte alpha = 255) : this(grayscale, grayscale, grayscale, alpha) { }

    /// <summary>
    /// From packed 0xRRGGBBAA
    /// </summary>
    /// <param name="rgba"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorB(uint rgba)
        : this(
            (byte)(rgba >> 24 & 0xFF),
            (byte)(rgba >> 16 & 0xFF),
            (byte)(rgba >>  8 & 0xFF),
            (byte)(rgba       & 0xFF)
        )
    { }

    /// <summary>
    /// Supports "#RRGGBB", "#RRGGBBAA", "#AARRGGBB"
    /// </summary>
    /// <param name="hex"></param>
    /// <param name="type"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorB(string hex, HexType type = HexType.RRGGBBAA)
        : this(ParseHexToRgba(hex, type))
    { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ToRgba32() => (uint)R << 24 | (uint)G << 16 | (uint)B << 8 | A;

    public override string ToString() => $"ColorB(R:{R}, G:{G}, B:{B}, A:{A})";
    public string ToString(HexType type) => ParseRgbaToHex(ToRgba32(), type);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ColorB Lerp(in ColorB a, in ColorB b, float t) => ColorF.Lerp(a, b, t);

    public static uint ParseHexToRgba(string hex, HexType type = HexType.RRGGBBAA)
    {
        ArgumentNullException.ThrowIfNull(hex);
        if (hex.Length < 7 || hex[0] != '#')
            throw new ArgumentException("Must start with '#'.", nameof(hex));

        var s = hex.AsSpan(1);

        switch (s.Length)
        {
            case 6: {
                var rrggbb = Convert.ToUInt32(s.ToString(), 16);
                return rrggbb << 8 | 0xFFu; // add AA=FF
            }
            case 8: {
                var x = Convert.ToUInt32(s.ToString(), 16);

                return type switch
                {
                    HexType.RRGGBBAA => x,
                    HexType.AARRGGBB => (x & 0x00FFFFFFu) << 8 | x >> 24 & 0xFFu,
                    _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown HexType.")
                };
            }
            default: throw new ArgumentException("Use '#RRGGBB', '#RRGGBBAA', or '#AARRGGBB'.", nameof(hex));
        }
    }
    
    public static string ParseRgbaToHex(uint rgba, HexType type = HexType.RRGGBBAA)
    {
        return type switch
        {
            HexType.RRGGBBAA => $"#{rgba:X8}",
            HexType.AARRGGBB => $"#{rgba << 24 | rgba >> 8 & 0xFFFFFFu:X8}",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown HexType.")
        };
    }
}