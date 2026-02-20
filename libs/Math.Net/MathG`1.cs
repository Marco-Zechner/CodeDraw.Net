using System.Numerics;

namespace MarcoZechner.MathDotNet;

public static class MathG
{
    // =====================================================
    // Basic (INumber<T>)
    // =====================================================

    public static T Min<T>(T a, T b) where T : INumber<T> => T.Min(a, b);
    public static T Max<T>(T a, T b) where T : INumber<T> => T.Max(a, b);
    public static T Clamp<T>(T value, T min, T max) where T : INumber<T> => T.Clamp(value, min, max);

    public static T Abs<T>(T value) where T : INumber<T> => T.Abs(value);

    public static bool IsZero<T>(T value) where T : INumber<T> => value == T.Zero;
    public static bool IsNonZero<T>(T value) where T : INumber<T> => value != T.Zero;
    public static bool IsPositive<T>(T value) where T : INumber<T> => value > T.Zero;
    public static bool IsNegative<T>(T value) where T : INumber<T> => value < T.Zero;
    public static bool IsNonNegative<T>(T value) where T : INumber<T> => value >= T.Zero;
    public static bool IsNonPositive<T>(T value) where T : INumber<T> => value <= T.Zero;

    /// <summary>Returns -1, 0, +1.</summary>
    public static int Sign<T>(T value) where T : INumber<T>
        => value > T.Zero ? 1 : value < T.Zero ? -1 : 0;

    /// <summary>Linear interpolation: a + (b-a)*t.</summary>
    public static T Lerp<T>(T a, T b, T t) where T : INumber<T> => a + (b - a) * t;

    /// <summary>Inverse lerp (no clamp). Throws if a == b.</summary>
    public static T InverseLerp<T>(T a, T b, T value) where T : INumber<T>
    {
        var denom = b - a;
        if (denom == T.Zero) throw new DivideByZeroException("InverseLerp requires a!=b.");
        return (value - a) / denom;
    }

    /// <summary>Maps value from [inMin,inMax] to [outMin,outMax] (no clamp).</summary>
    public static T Map<T>(T value, T inMin, T inMax, T outMin, T outMax) where T : INumber<T>
    {
        var t = InverseLerp(inMin, inMax, value);
        return Lerp(outMin, outMax, t);
    }

    /// <summary>Maps value from [inMin,inMax] to [outMin,outMax], clamping to [outMin,outMax].</summary>
    public static T MapClamped<T>(T value, T inMin, T inMax, T outMin, T outMax) where T : INumber<T>
    {
        var t = Clamp(InverseLerp(inMin, inMax, value), T.Zero, T.One);
        return Lerp(outMin, outMax, t);
    }

    /// <summary>Step: x &lt; edge ? 0 : 1.</summary>
    public static T Step<T>(T edge, T x) where T : INumber<T> => x < edge ? T.Zero : T.One;

    /// <summary>
    /// SmoothStep over [0,1]. x is clamped to [0,1]. Polynomial: x*x*(3-2x)
    /// </summary>
    public static T SmoothStep<T>(T x) where T : INumber<T>
    {
        x = Clamp(x, T.Zero, T.One);
        return x * x * (T.CreateChecked(3) - T.CreateChecked(2) * x);
    }

    /// <summary>SmoothStep from edge0..edge1.</summary>
    public static T SmoothStep<T>(T edge0, T edge1, T x) where T : INumber<T>
    {
        var t = Clamp(InverseLerp(edge0, edge1, x), T.Zero, T.One);
        return SmoothStep(t);
    }

    /// <summary>Wrap value to [min,max). Requires max &gt; min.</summary>
    public static T Wrap<T>(T value, T min, T max) where T : INumber<T>
    {
        var range = max - min;
        if (range <= T.Zero) throw new ArgumentOutOfRangeException(nameof(max), "Wrap requires max>min.");
        return min + (value - min) % range;
    }

    // =====================================================
    // Float-return convenience (keep for Vector2<T> etc.)
    // =====================================================

    public static float SinF<T>(T x, AngleUnit angleUnit = AngleUnit.Degrees) where T : INumber<T>
        => float.Sin(AngleToRadiansF(x, angleUnit));

    public static float CosF<T>(T x, AngleUnit angleUnit = AngleUnit.Degrees) where T : INumber<T>
        => float.Cos(AngleToRadiansF(x, angleUnit));

    public static float TanF<T>(T x, AngleUnit angleUnit = AngleUnit.Degrees) where T : INumber<T>
        => float.Tan(AngleToRadiansF(x, angleUnit));

    public static float AsinF<T>(T x, AngleUnit angleUnit = AngleUnit.Degrees) where T : INumber<T>
        => RadiansToAngleF(float.Asin(ToFloat(x)), angleUnit);

    public static float AcosF<T>(T x, AngleUnit angleUnit = AngleUnit.Degrees) where T : INumber<T>
        => RadiansToAngleF(float.Acos(ToFloat(x)), angleUnit);

    public static float AtanF<T>(T x, AngleUnit angleUnit = AngleUnit.Degrees) where T : INumber<T>
        => RadiansToAngleF(float.Atan(ToFloat(x)), angleUnit);

    public static float Atan2F<T>(T y, T x, AngleUnit angleUnit = AngleUnit.Degrees) where T : INumber<T>
        => RadiansToAngleF(float.Atan2(ToFloat(y), ToFloat(x)), angleUnit);

    public static float SqrtF<T>(T x) where T : INumber<T> => float.Sqrt(ToFloat(x));
    public static float PowF<T>(T x, T y) where T : INumber<T> => float.Pow(ToFloat(x), ToFloat(y));
    public static float ExpF<T>(T x) where T : INumber<T> => float.Exp(ToFloat(x));
    public static float LogF<T>(T x) where T : INumber<T> => float.Log(ToFloat(x));
    public static float LogF<T>(T x, T newBase) where T : INumber<T> => float.Log(ToFloat(x), ToFloat(newBase));

    public static float ToRadiansF<T>(T degrees) where T : INumber<T> => ToFloat(degrees) * ToFloat(Math.PI / 180.0);
    public static float ToDegreesF<T>(T radians) where T : INumber<T> => ToFloat(radians) * ToFloat(180.0 / Math.PI);
    
    private static float AngleToRadiansF<T>(T angle, AngleUnit unit) where T : INumber<T>
    {
        var a = ToFloat(angle);
        return unit == AngleUnit.Degrees ? a * (MathF.PI / 180f) : a;
    }
    
    private static float RadiansToAngleF(float radians, AngleUnit unit)
        => unit == AngleUnit.Degrees ? radians * (180f / MathF.PI) : radians;

    // =====================================================
    // Trig - typed output (output-only generic, input is double)
    // (rounding ONLY happens when TOut is integral)
    // =====================================================

    public static TOut Sin<TOut>(double x, AngleUnit angleUnit = AngleUnit.Degrees) where TOut : INumber<TOut>
        => FromDouble<TOut>(double.Sin(AngleToRadians(x, angleUnit)));

    public static TOut Cos<TOut>(double x, AngleUnit angleUnit = AngleUnit.Degrees) where TOut : INumber<TOut>
        => FromDouble<TOut>(double.Cos(AngleToRadians(x, angleUnit)));

    public static TOut Tan<TOut>(double x, AngleUnit angleUnit = AngleUnit.Degrees) where TOut : INumber<TOut>
        => FromDouble<TOut>(double.Tan(AngleToRadians(x, angleUnit)));

    public static TOut Asin<TOut>(double x, AngleUnit angleUnit = AngleUnit.Degrees) where TOut : INumber<TOut>
        => FromDouble<TOut>(RadiansToAngle(double.Asin(x), angleUnit));

    public static TOut Acos<TOut>(double x, AngleUnit angleUnit = AngleUnit.Degrees) where TOut : INumber<TOut>
        => FromDouble<TOut>(RadiansToAngle(double.Acos(x), angleUnit));

    public static TOut Atan<TOut>(double x, AngleUnit angleUnit = AngleUnit.Degrees) where TOut : INumber<TOut>
        => FromDouble<TOut>(RadiansToAngle(double.Atan(x), angleUnit));

    public static TOut Atan2<TOut>(double y, double x, AngleUnit angleUnit = AngleUnit.Degrees) where TOut : INumber<TOut>
        => FromDouble<TOut>(RadiansToAngle(double.Atan2(y, x), angleUnit));
    
    
    public static TOut Sqrt<TOut>(double x) where TOut : INumber<TOut> => FromDouble<TOut>(double.Sqrt(x));
    public static TOut Pow<TOut>(double x, double y) where TOut : INumber<TOut> => FromDouble<TOut>(double.Pow(x, y));
    public static TOut Exp<TOut>(double x) where TOut : INumber<TOut> => FromDouble<TOut>(double.Exp(x));
    public static TOut Log<TOut>(double x) where TOut : INumber<TOut> => FromDouble<TOut>(double.Log(x));
    public static TOut Log<TOut>(double x, double newBase) where TOut : INumber<TOut> => FromDouble<TOut>(double.Log(x, newBase));

    public static TOut ToRadians<TOut>(double degrees) where TOut : INumber<TOut> => FromDouble<TOut>(degrees * (Math.PI / 180.0));
    public static TOut ToDegrees<TOut>(double radians) where TOut : INumber<TOut> => FromDouble<TOut>(radians * (180.0 / Math.PI));
    
    private static double AngleToRadians(double angle, AngleUnit unit)
        => unit == AngleUnit.Degrees ? angle * (Math.PI / 180.0) : angle;
    
    private static double RadiansToAngle(double radians, AngleUnit unit)
        => unit == AngleUnit.Degrees ? radians * (180.0 / Math.PI) : radians;
    
    // =====================================================
    // Approx (double-domain semantics)
    // =====================================================

    public static bool Approximately<T>(T a, T b, T eps) where T : INumber<T>
        => Math.Abs(ToDouble(a) - ToDouble(b)) <= Math.Abs(ToDouble(eps));

    public static bool ApproximatelyZero<T>(T a, T eps) where T : INumber<T>
        => Math.Abs(ToDouble(a)) <= Math.Abs(ToDouble(eps));
    
    // =====================================================
    // Internals
    // =====================================================

    private static bool IsIntegralType<T>()
    {
        var t = typeof(T);
        return t == typeof(sbyte) || t == typeof(byte) ||
               t == typeof(short) || t == typeof(ushort) ||
               t == typeof(int) || t == typeof(uint) ||
               t == typeof(long) || t == typeof(ulong) ||
               t == typeof(nint) || t == typeof(nuint) ||
               t == typeof(BigInteger);
    }

    internal static T FromDouble<T>(double v) where T : INumber<T>
    {
        if (IsIntegralType<T>())
            v = double.Round(v, MidpointRounding.AwayFromZero);

        return T.CreateChecked(v);
    }
    
    internal static double ToDouble<TAny>(TAny v) where TAny : INumber<TAny> => double.CreateChecked(v);
    internal static float ToFloat<TAny>(TAny v) where TAny : INumber<TAny> => float.CreateChecked(v);
}