using SkiaSharp;

namespace MarcoZechner.MathDotNet;

public static class MatrixSkiaExtensions
{
    public static SKMatrix ToSkia(this Matrix3x3 m) => new()
    {
        ScaleX = m.M11, SkewX = m.M12, TransX = m.M13,
        SkewY  = m.M21, ScaleY = m.M22, TransY = m.M23,
        Persp0 = m.M31, Persp1 = m.M32, Persp2 = m.M33
    };

    public static Matrix3x3 ToMatrix3x3(this SKMatrix m) => new(
        m.ScaleX, m.SkewX, m.TransX,
        m.SkewY,  m.ScaleY, m.TransY,
        m.Persp0, m.Persp1, m.Persp2
    );
}
