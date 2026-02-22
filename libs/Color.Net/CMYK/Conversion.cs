using System.Runtime.CompilerServices;
using MarcoZechner.ColorDotNet.HSV;
using MarcoZechner.ColorDotNet.RGB;

namespace MarcoZechner.ColorDotNet.CMYK;

public partial record struct ColorCmykF
{
    // =========================================================
    // Conversions layer: CMYK_F knows RGB_F directly.
    // CMYK_F <-> RGB_F
    // CMYK_F <-> HSV_F (via RGB_F)
    // CMYK_F <-> RGB_B (via RGB_F)
    // CMYK_F <-> HSV_B (via RGB_F, HSV_F)
    // =========================================================

    // CMYK_F <-> RGB_F (direct)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorCmykF(ColorF rgbF) => FromRgb(rgbF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorF(ColorCmykF cmykF) => cmykF.ToRgb();

    // CMYK_F <-> HSV_F (via RGB_F)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorCmykF(ColorHsvF hsvF) => (ColorF)hsvF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorHsvF(ColorCmykF cmykF) => (ColorF)cmykF;

    // CMYK_F <-> RGB_B (via RGB_F)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorCmykF(ColorB rgbB) => (ColorF)rgbB;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorB(ColorCmykF cmykF) => (ColorF)cmykF;

    // CMYK_F <-> HSV_B (via RGB_F, HSV_F)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorCmykF(ColorHsvB hsvB) => (ColorF)(ColorHsvF)hsvB;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorHsvB(ColorCmykF cmykF) => (ColorHsvF)(ColorF)cmykF;
}

public partial record struct ColorCmykB
{
    // =========================================================
    // Conversions layer: CMYK_B knows CMYK_F, and bridges to RGB/HSV.
    // CMYK_B <-> CMYK_F
    // CMYK_B <-> RGB_F (via CMYK_F)
    // CMYK_B <-> HSV_F (via CMYK_F, RGB_F)
    // CMYK_B <-> RGB_B (via CMYK_F, RGB_F)
    // CMYK_B <-> HSV_B (via CMYK_F, RGB_F, HSV_F)
    // =========================================================

    // CMYK_B <-> CMYK_F (direct)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorCmykF(ColorCmykB cmykB) => cmykB.ToCmykF();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorCmykB(ColorCmykF cmykF) => FromCmykF(cmykF);

    // CMYK_B <-> RGB_F (via CMYK_F)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorF(ColorCmykB cmykB) => ((ColorCmykF)cmykB).ToRgb();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorCmykB(ColorF rgbF) => FromCmykF(rgbF);

    // CMYK_B <-> HSV_F (via CMYK_F, RGB_F)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorHsvF(ColorCmykB cmykB) => (ColorF)cmykB;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorCmykB(ColorHsvF hsvF) => (ColorF)hsvF;

    // CMYK_B <-> RGB_B (via CMYK_F, RGB_F)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorB(ColorCmykB cmykB) => (ColorF)cmykB;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorCmykB(ColorB rgbB) => (ColorF)rgbB;

    // CMYK_B <-> HSV_B (via CMYK_F, RGB_F, HSV_F)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorHsvB(ColorCmykB cmykB) => (ColorHsvF)(ColorF)cmykB;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorCmykB(ColorHsvB hsvB) => (ColorF)(ColorHsvF)hsvB;
}