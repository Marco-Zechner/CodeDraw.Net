using SkiaSharp;

namespace MarcoZechner.Math;

public readonly partial record struct Matrix3x3{
    public static implicit operator SKMatrix(Matrix3x3 m) => new(
        m.M11, m.M12, m.M13,
        m.M21, m.M22, m.M23,
        m.M31, m.M32, m.M33);
    public static implicit operator Matrix3x3(SKMatrix m) => new(
        m.Values[0], m.Values[1], m.Values[2],
        m.Values[3], m.Values[4], m.Values[5],
        m.Values[6], m.Values[7], m.Values[8]);
}