using System.Runtime.CompilerServices;

namespace MarcoZechner.ColorDotNet.CMYK;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly partial record struct ColorCmykB(byte C, byte M, byte Y, byte K, byte A = 255)
{
    public override string ToString() => $"ColorCmykB(C:{C}, M:{M}, Y:{Y}, K:{K}, A:{A})";

    // Core conversion: CMYK_B <-> CMYK_F (quantize / dequantize)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ColorCmykB FromCmykF(ColorCmykF cmyk)
        => new(
            ToByteClamped(cmyk.C),
            ToByteClamped(cmyk.M),
            ToByteClamped(cmyk.Y),
            ToByteClamped(cmyk.K),
            ToByteClamped(cmyk.A)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ColorCmykF ToCmykF()
        => new(
            C * (1f / 255f),
            M * (1f / 255f),
            Y * (1f / 255f),
            K * (1f / 255f),
            A * (1f / 255f)
        );

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