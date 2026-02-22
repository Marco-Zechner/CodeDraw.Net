using System.Globalization;
using System.Runtime.CompilerServices;

namespace MarcoZechner.ColorDotNet;

public static class ColorCodec
{
    /// ========================================================= <br/>
    /// Parse: "#..." -> RGBA32 (packed as 0xRRGGBBAA) <br/>
    /// Supports: <br/>
    ///   #RGB, #RGBA, #ARGB, #RRGGBB, #RRGGBBAA, #AARRGGBB <br/>
    /// =========================================================
    internal static uint ParseHexToRgba(string hex, ColorLayout layout = ColorLayout.RRGGBBAA)
    {
        ArgumentNullException.ThrowIfNull(hex);

        if (hex.Length < 2 || hex[0] != '#')
            throw new ArgumentException("Must start with '#'.", nameof(hex));

        var s = hex.AsSpan(1);

        // fast-ish: parse once, then rearrange
        var x = ParseHexUInt(s);

        switch (s.Length)
        {
            // --- 3 digits: #RGB ---
            case 3:
            {
                if (layout != ColorLayout.RGB)
                    throw new ArgumentException("3-digit hex requires layout RGB (use '#RGB').", nameof(layout));

                var r = Expand4To8((byte)(x >> 8 & 0xFu));
                var g = Expand4To8((byte)(x >> 4 & 0xFu));
                var b = Expand4To8((byte)(x >> 0 & 0xFu));
                return PackRgba(r, g, b, 0xFF);
            }

            // --- 4 digits: #RGBA or #ARGB ---
            case 4:
            {
                switch (layout)
                {
                    case ColorLayout.RGBA: {
                        var r = Expand4To8((byte)(x >> 12 & 0xFu));
                        var g = Expand4To8((byte)(x >> 8 & 0xFu));
                        var b = Expand4To8((byte)(x >> 4 & 0xFu));
                        var a = Expand4To8((byte)(x >> 0 & 0xFu));
                        return PackRgba(r, g, b, a);
                    }
                    case ColorLayout.ARGB: {
                        var a = Expand4To8((byte)(x >> 12 & 0xFu));
                        var r = Expand4To8((byte)(x >> 8 & 0xFu));
                        var g = Expand4To8((byte)(x >> 4 & 0xFu));
                        var b = Expand4To8((byte)(x >> 0 & 0xFu));
                        return PackRgba(r, g, b, a);
                    }
                    
                    default: throw new ArgumentException("4-digit hex requires layout RGBA or ARGB (use '#RGBA' or '#ARGB').", nameof(layout));
                }
            }

            // --- 6 digits: #RRGGBB ---
            case 6:
            {
                if (layout != ColorLayout.RRGGBB)
                    throw new ArgumentException("6-digit hex requires layout RRGGBB (use '#RRGGBB').", nameof(layout));

                var r = (byte)(x >> 16 & 0xFFu);
                var g = (byte)(x >> 8 & 0xFFu);
                var b = (byte)(x >> 0 & 0xFFu);
                return PackRgba(r, g, b, 0xFF);
            }

            // --- 8 digits: #RRGGBBAA or #AARRGGBB ---
            case 8:
            {
                return layout switch
                {
                    ColorLayout.RRGGBBAA =>
                        // x is already 0xRRGGBBAA, which matches our internal RGBA32 pack
                        x,

                    ColorLayout.AARRGGBB =>
                        // x is 0xAARRGGBB; rearrange to 0xRRGGBBAA
                        (x & 0x00FFFFFFu) << 8 | x >> 24 & 0xFFu,

                    _ => throw new ArgumentException("8-digit hex requires layout RRGGBBAA or AARRGGBB (use '#RRGGBBAA' or '#AARRGGBB').", nameof(layout))
                };
            }

            default:
                throw new ArgumentException("Use '#RGB', '#RGBA', '#ARGB', '#RRGGBB', '#RRGGBBAA', or '#AARRGGBB'.", nameof(hex));
        }
    }

    // =========================================================
    // Format: RGBA32 (0xRRGGBBAA) -> "#..."
    // =========================================================
    internal static string ParseRgbaToHex(uint rgba, ColorLayout layout = ColorLayout.RRGGBBAA)
    {
        var r = (byte)(rgba >> 24);
        var g = (byte)(rgba >> 16);
        var b = (byte)(rgba >> 8);
        var a = (byte)(rgba >> 0);

        return layout switch
        {
            ColorLayout.RGB      => $"#{ToHexNibble(r)}{ToHexNibble(g)}{ToHexNibble(b)}",
            ColorLayout.RGBA     => $"#{ToHexNibble(r)}{ToHexNibble(g)}{ToHexNibble(b)}{ToHexNibble(a)}",
            ColorLayout.ARGB     => $"#{ToHexNibble(a)}{ToHexNibble(r)}{ToHexNibble(g)}{ToHexNibble(b)}",

            ColorLayout.RRGGBB   => $"#{r:X2}{g:X2}{b:X2}",
            ColorLayout.RRGGBBAA => $"#{rgba:X8}",

            ColorLayout.AARRGGBB => $"#{(uint)a << 24 | (uint)r << 16 | (uint)g << 8 | b:X8}",

            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, "Unknown ColorLayout.")
        };
    }

    // =========================================================
    // Convert to uint in requested layout
    // Supports:
    //   0xFFF       (RGB)
    //   0xFFFF      (ARGB or RGBA)
    //   0xFFFFFF    (RRGGBB)
    //   0xFFFFFFFF  (AARRGGBB or RRGGBBAA)
    // =========================================================
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint ToPackedUInt(byte r, byte g, byte b, byte a, ColorLayout layout)
    {
        return layout switch
        {
            // 3-digit packed nibbles: 0xRGB
            ColorLayout.RGB =>
                (uint)(ToNibble(r) << 8 | ToNibble(g) << 4 | ToNibble(b)),

            // 4-digit packed nibbles: 0xRGBA
            ColorLayout.RGBA =>
                (uint)(ToNibble(r) << 12 | ToNibble(g) << 8 | ToNibble(b) << 4 | ToNibble(a)),

            // 4-digit packed nibbles: 0xARGB
            ColorLayout.ARGB =>
                (uint)(ToNibble(a) << 12 | ToNibble(r) << 8 | ToNibble(g) << 4 | ToNibble(b)),

            // 6-digit packed bytes: 0xRRGGBB
            ColorLayout.RRGGBB =>
                (uint)r << 16 | (uint)g << 8 | b,

            // 8-digit packed bytes: 0xRRGGBBAA
            ColorLayout.RRGGBBAA =>
                (uint)r << 24 | (uint)g << 16 | (uint)b << 8 | a,

            // 8-digit packed bytes: 0xAARRGGBB
            ColorLayout.AARRGGBB =>
                (uint)a << 24 | (uint)r << 16 | (uint)g << 8 | b,

            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, "Unknown ColorLayout.")
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (byte r, byte g, byte b, byte a) UnpackToRgba32(uint packed, ColorLayout layout)
    {
        return layout switch
        {
            // 0xRGB
            ColorLayout.RGB => (
                Expand4To8((byte)(packed >> 8 & 0xFu)),
                Expand4To8((byte)(packed >> 4 & 0xFu)),
                Expand4To8((byte)(packed >> 0 & 0xFu)),
                0xFF
            ),

            // 0xRGBA
            ColorLayout.RGBA => (
                Expand4To8((byte)(packed >> 12 & 0xFu)),
                Expand4To8((byte)(packed >> 8 & 0xFu)),
                Expand4To8((byte)(packed >> 4 & 0xFu)),
                Expand4To8((byte)(packed >> 0 & 0xFu))
            ),

            // 0xARGB
            ColorLayout.ARGB => (
                Expand4To8((byte)(packed >> 8 & 0xFu)),
                Expand4To8((byte)(packed >> 4 & 0xFu)),
                Expand4To8((byte)(packed >> 0 & 0xFu)),
                Expand4To8((byte)(packed >> 12 & 0xFu))
            ),

            // 0xRRGGBB
            ColorLayout.RRGGBB => (
                (byte)(packed >> 16 & 0xFF),
                (byte)(packed >> 8 & 0xFF),
                (byte)(packed & 0xFF),
                0xFF
            ),

            // 0xRRGGBBAA
            ColorLayout.RRGGBBAA => (
                (byte)(packed >> 24 & 0xFF),
                (byte)(packed >> 16 & 0xFF),
                (byte)(packed >> 8 & 0xFF),
                (byte)(packed & 0xFF)
            ),

            // 0xAARRGGBB
            ColorLayout.AARRGGBB => (
                (byte)(packed >> 16 & 0xFF),
                (byte)(packed >> 8 & 0xFF),
                (byte)(packed & 0xFF),
                (byte)(packed >> 24 & 0xFF)
            ),

            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, "Unknown ColorLayout.")
        };
    }
    
    // Convenience: internal canonical pack (0xRRGGBBAA)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint PackRgba(byte r, byte g, byte b, byte a)
        => (uint)r << 24 | (uint)g << 16 | (uint)b << 8 | a;

    // ============================
    // Helpers
    // ============================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Expand4To8(byte n4) => (byte)(n4 << 4 | n4);

    // Quantize 8-bit channel to hex nibble (0..15).
    // This is the only “policy” decision here.
    // Using round-to-nearest (not floor) produces nicer symmetry with Expand4To8.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ToNibble(byte c8) => (c8 * 15 + 127) / 255;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char ToHexNibble(byte c8)
        => "0123456789ABCDEF"[ToNibble(c8)];

    // Parse hex span (length 3/4/6/8) into uint without allocations.
    private static uint ParseHexUInt(ReadOnlySpan<char> s) 
        => uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var x) 
        ? x 
        : throw new ArgumentException("Invalid hex digits.", nameof(s));
}