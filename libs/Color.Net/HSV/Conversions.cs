using System.Runtime.CompilerServices;
using MarcoZechner.ColorDotNet.RGB;

namespace MarcoZechner.ColorDotNet.HSV;

public partial record struct ColorHsvF
{
    // =========================================================
    // Conversions layer: HSV_F knows RGB_F directly.
    // HSV_F <-> RGB_F
    // HSV_F <-> RGB_B (via RGB_F)
    // =========================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorHsvF(ColorF rgb) => FromRgb(rgb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorF(ColorHsvF hsv) => hsv.ToRgb();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorHsvF(ColorB rgbB) => FromRgb(rgbB);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorB(ColorHsvF hsv) => hsv.ToRgb();
}

public partial record struct ColorHsvB
{
    // =========================================================
    // Conversions layer: HSV_B knows HSV_F, and bridges to RGB.
    // HSV_B <-> HSV_F
    // HSV_B <-> RGB_F (via HSV_F)
    // HSV_B <-> RGB_B (via HSV_F and RGB_F)
    // =========================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorHsvF(ColorHsvB hsvB) => hsvB.ToHsvF();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorHsvB(ColorHsvF hsvF) => FromHsvF(hsvF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorF(ColorHsvB hsvB) => ((ColorHsvF)hsvB).ToRgb();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorHsvB(ColorF rgbF) => FromHsvF(rgbF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorB(ColorHsvB hsvB) => (ColorF)hsvB;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorHsvB(ColorB rgbB) => (ColorF)rgbB;
}