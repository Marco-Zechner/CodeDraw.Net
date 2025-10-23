using System.Numerics;

namespace MarcoZechner.Math;

public static class MathG
{
    public static T Max<T>(T a, T b) where T : INumber<T> => a > b ? a : b;
    public static T Min<T>(T a, T b) where T : INumber<T> => a < b ? a : b;
    public static T Clamp<T>(T value, T min, T max) where T : INumber<T> => Max(min, Min(value, max));
    public static bool IsZero<T>(T value) where T : INumber<T> => value == T.Zero;
    public static bool IsPositive<T>(T value) where T : INumber<T> => value > T.Zero;
    public static bool IsNegative<T>(T value) where T : INumber<T> => value < T.Zero;
    public static T Lerp<T>(T a, T b, T t) where T : INumber<T> => a + (b - a) * t;
    public static T Abs<T>(T value) where T : INumber<T> => IsNegative(value) ? -value : value;
}