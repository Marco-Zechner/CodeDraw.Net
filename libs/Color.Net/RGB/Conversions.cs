using System.Runtime.CompilerServices;

namespace MarcoZechner.ColorDotNet.RGB;

public partial record struct ColorB
{
    // Conversions (RGB only)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorB(uint rgba) => new(rgba, ColorLayout.RRGGBBAA);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint(ColorB c) => c.ToRgba32(ColorLayout.RRGGBBAA);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorF(ColorB c) => new(c);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorB(ColorF c)
    {
        return new ColorB(ToByteClamped(c.R), ToByteClamped(c.G), ToByteClamped(c.B), ToByteClamped(c.A));

        static byte ToByteClamped(float x)
        {
            return x switch {
                <= 0f => 0,
                >= 1f => 255,
                _ => (byte)(x * 255f + 0.5f)
            };
        }
    }
}

public partial record struct ColorF
{
    // Conversions (RGB only)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ColorF(uint rgba) => new(rgba, ColorLayout.RRGGBBAA);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint(ColorF c) => c.ToRgba32(ColorLayout.RRGGBBAA);


}