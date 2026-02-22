using System.Runtime.CompilerServices;

namespace MarcoZechner.ColorDotNet.HSV;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly partial record struct ColorHsvB(int H, byte S, byte V, byte A = 255)
{
    public override string ToString() => $"ColorHsvB(H:{H}, S:{S}, V:{V}, A:{A})";

    // Core conversion: HSV_B <-> HSV_F (quantize / dequantize)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ColorHsvB FromHsvF(ColorHsvF hsv)
        => new(hsv.H, ToByteClamped(hsv.S), ToByteClamped(hsv.V), ToByteClamped(hsv.A));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorHsvF ToHsvF()
        => new(H, S * (1f / 255f), V * (1f / 255f), A * (1f / 255f));

    private static byte ToByteClamped(float x)
    {
        return x switch
        {
            <= 0f => 0,
            >= 1f => 255,
            _ => (byte)(x * 255f + 0.5f)
        };
    }
}