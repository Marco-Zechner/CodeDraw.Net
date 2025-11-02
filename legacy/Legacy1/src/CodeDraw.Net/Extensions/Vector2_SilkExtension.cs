using MarcoZechner.MathDotNet;
using Silk.NET.Maths;

namespace MarcoZechner.CodeDrawDotNet.Extensions;

public static class VectorSilkExtensions
{
    // float
    public static Vector2D<float> ToSilk(this Vector2 v) => new(v.X, v.Y);
    public static Vector2 ToVector2(this Vector2D<float> v) => new((float)v.X, (float)v.Y);

    // double
    public static Vector2D<double> ToSilkD(this Vector2 v) => new(v.X, v.Y);
    public static Vector2 ToVector2(this Vector2D<double> v) => new((float)v.X, (float)v.Y);

    // int
    public static Vector2D<int> ToSilkI(this Vector2 v) => new((int)v.X, (int)v.Y);
    public static Vector2 ToVector2(this Vector2D<int> v) => new(v.X, v.Y);

    // generic
    public static Vector2D<T> ToSilk<T>(this Vector2<T> v) where T : unmanaged, System.Numerics.INumber<T>
        => new(v.X, v.Y);

    public static Vector2<T> ToVector2<T>(this Vector2D<T> v) where T : unmanaged, System.Numerics.INumber<T>
        => new(v.X, v.Y);
}
