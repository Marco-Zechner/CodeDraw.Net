using System.Numerics;

namespace MarcoZechner.MathDotNet;

public readonly partial record struct Vector2
{
#region Returns Number

    public static float DistanceSquared<TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => Vector2<float>.DistanceSquared(a, b);

    public static TOut DistanceSquared<TOut, TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => Vector2<float>.DistanceSquared<TOut, TA, TB>(a, b);

    public static float DistanceSquared(Vector2<double> a, Vector2<double> b)
        => Vector2<float>.DistanceSquared(a, b);

    public static TOut DistanceSquared<TOut>(Vector2<double> a, Vector2<double> b)
        where TOut : unmanaged, INumber<TOut>
        => Vector2<double>.DistanceSquared<TOut>(a, b);


    public static float Distance<TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => Vector2<float>.Distance(a, b);

    public static TOut Distance<TOut, TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => Vector2<float>.Distance<TOut, TA, TB>(a, b);

    public static float Distance(Vector2<double> a, Vector2<double> b)
        => Vector2<float>.Distance(a, b);

    public static TOut Distance<TOut>(Vector2<double> a, Vector2<double> b)
        where TOut : unmanaged, INumber<TOut>
        => Vector2<double>.Distance<TOut>(a, b);


    public static float Dot<TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => Vector2<float>.Dot(a, b);

    public static TOut Dot<TOut, TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => Vector2<float>.Dot<TOut, TA, TB>(a, b);

    public static float Dot(Vector2<double> a, Vector2<double> b)
        => Vector2<float>.Dot(a, b);

    public static TOut Dot<TOut>(Vector2<double> a, Vector2<double> b)
        where TOut : unmanaged, INumber<TOut>
        => Vector2<double>.Dot<TOut>(a, b);


    public static float CrossZ<TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => Vector2<float>.CrossZ(a, b);

    public static TOut CrossZ<TOut, TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => Vector2<float>.CrossZ<TOut, TA, TB>(a, b);

    public static float CrossZ(Vector2<double> a, Vector2<double> b)
        => Vector2<float>.CrossZ(a, b);

    public static TOut CrossZ<TOut>(Vector2<double> a, Vector2<double> b)
        where TOut : unmanaged, INumber<TOut>
        => Vector2<double>.CrossZ<TOut>(a, b);


    public static float AngleBetween<TA, TB>(Vector2<TA> a, Vector2<TB> b, AngleUnit angleUnit = AngleUnit.Degrees)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => Vector2<float>.AngleBetween(a, b, angleUnit);

    public static TOut AngleBetween<TOut, TA, TB>(Vector2<TA> a, Vector2<TB> b, AngleUnit angleUnit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => Vector2<float>.AngleBetween<TOut, TA, TB>(a, b, angleUnit);

    public static float AngleBetween(Vector2<double> a, Vector2<double> b, AngleUnit angleUnit = AngleUnit.Degrees)
        => Vector2<float>.AngleBetween(a, b, angleUnit);

    public static TOut AngleBetween<TOut>(Vector2<double> a, Vector2<double> b, AngleUnit angleUnit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        => Vector2<double>.AngleBetween<TOut>(a, b, angleUnit);

#endregion

#region Returns Vector2<TOut>

    public static Vector2<float> MinF<TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => Vector2<float>.Min(a, b);

    public static Vector2<TOut> Min<TOut, TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => Vector2<float>.Min<TOut, TA, TB>(a, b);

    public static Vector2<float> Min(Vector2<double> a, Vector2<double> b)
        => Vector2<float>.Min(a, b);

    public static Vector2<TOut> Min<TOut>(Vector2<double> a, Vector2<double> b)
        where TOut : unmanaged, INumber<TOut>
        => Vector2<double>.Min<TOut>(a, b);


    public static Vector2<float> Max<TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => Vector2<float>.Max(a, b);

    public static Vector2<TOut> Max<TOut, TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => Vector2<float>.Max<TOut, TA, TB>(a, b);

    public static Vector2<float> Max(Vector2<double> a, Vector2<double> b)
        => Vector2<float>.Max(a, b);

    public static Vector2<TOut> Max<TOut>(Vector2<double> a, Vector2<double> b)
        where TOut : unmanaged, INumber<TOut>
        => Vector2<double>.Max<TOut>(a, b);


    public static Vector2<float> Clamp<TV, TMin, TMax>(Vector2<TV> v, Vector2<TMin> min, Vector2<TMax> max)
        where TV : unmanaged, INumber<TV>
        where TMin : unmanaged, INumber<TMin>
        where TMax : unmanaged, INumber<TMax>
        => Vector2<float>.Clamp(v, min, max);

    public static Vector2<TOut> Clamp<TOut, TV, TMin, TMax>(Vector2<TV> v, Vector2<TMin> min, Vector2<TMax> max)
        where TOut : unmanaged, INumber<TOut>
        where TV : unmanaged, INumber<TV>
        where TMin : unmanaged, INumber<TMin>
        where TMax : unmanaged, INumber<TMax>
        => Vector2<float>.Clamp<TOut, TV, TMin, TMax>(v, min, max);

    public static Vector2<float> Clamp(Vector2<double> v, Vector2<double> min, Vector2<double> max)
        => Vector2<float>.Clamp(v, min, max);

    public static Vector2<TOut> Clamp<TOut>(Vector2<double> v, Vector2<double> min, Vector2<double> max)
        where TOut : unmanaged, INumber<TOut>
        => Vector2<double>.Clamp<TOut>(v, min, max);


    public static Vector2<float> Lerp<TA, TB, TT>(Vector2<TA> a, Vector2<TB> b, TT t)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        where TT : INumber<TT>
        => Vector2<float>.Lerp(a, b, t);

    public static Vector2<TOut> Lerp<TOut, TA, TB, TT>(Vector2<TA> a, Vector2<TB> b, TT t)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        where TT : INumber<TT>
        => Vector2<float>.Lerp<TOut, TA, TB, TT>(a, b, t);

    public static Vector2<float> Lerp(Vector2<double> a, Vector2<double> b, double t)
        => Vector2<float>.Lerp(a, b, t);

    public static Vector2<TOut> Lerp<TOut>(Vector2<double> a, Vector2<double> b, double t)
        where TOut : unmanaged, INumber<TOut>
        => Vector2<double>.Lerp<TOut>(a, b, t);


    public static Vector2<float> Reflect<TV, TN>(Vector2<TV> v, Vector2<TN> normal)
        where TV : unmanaged, INumber<TV>
        where TN : unmanaged, INumber<TN>
        => Vector2<float>.Reflect(v, normal);

    public static Vector2<TOut> Reflect<TOut, TV, TN>(Vector2<TV> v, Vector2<TN> normal)
        where TOut : unmanaged, INumber<TOut>
        where TV : unmanaged, INumber<TV>
        where TN : unmanaged, INumber<TN>
        => Vector2<float>.Reflect<TOut, TV, TN>(v, normal);

    public static Vector2<float> Reflect(Vector2<double> v, Vector2<double> normal)
        => Vector2<float>.Reflect(v, normal);

    public static Vector2<TOut> Reflect<TOut>(Vector2<double> v, Vector2<double> normal)
        where TOut : unmanaged, INumber<TOut>
        => Vector2<double>.Reflect<TOut>(v, normal);


    public static Vector2<float> PerpendicularCcw<TA>(Vector2<TA> v)
        where TA : unmanaged, INumber<TA>
        => Vector2<float>.PerpendicularCcw(v);

    public static Vector2<TOut> PerpendicularCcw<TOut, TA>(Vector2<TA> v)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        => Vector2<float>.PerpendicularCcw<TOut, TA>(v);

    public static Vector2<float> PerpendicularCcw(Vector2<double> v)
        => Vector2<float>.PerpendicularCcw(v);

    public static Vector2<TOut> PerpendicularCcw<TOut>(Vector2<double> v)
        where TOut : unmanaged, INumber<TOut>
        => Vector2<double>.PerpendicularCcw<TOut>(v);


    public static Vector2<float> PerpendicularCw<TA>(Vector2<TA> v)
        where TA : unmanaged, INumber<TA>
        => Vector2<float>.PerpendicularCw(v);

    public static Vector2<TOut> PerpendicularCw<TOut, TA>(Vector2<TA> v)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        => Vector2<float>.PerpendicularCw<TOut, TA>(v);

    public static Vector2<float> PerpendicularCw(Vector2<double> v)
        => Vector2<float>.PerpendicularCw(v);

    public static Vector2<TOut> PerpendicularCw<TOut>(Vector2<double> v)
        where TOut : unmanaged, INumber<TOut>
        => Vector2<double>.PerpendicularCw<TOut>(v);


    public static Vector2<float> Rotate<TV, TAng>(Vector2<TV> v, TAng angle, AngleUnit angleUnit = AngleUnit.Degrees)
        where TV : unmanaged, INumber<TV>
        where TAng : INumber<TAng>
        => Vector2<float>.Rotate(v, angle, angleUnit);

    public static Vector2<TOut> Rotate<TOut, TV, TAng>(Vector2<TV> v, TAng angle, AngleUnit angleUnit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        where TV : unmanaged, INumber<TV>
        where TAng : INumber<TAng>
        => Vector2<float>.Rotate<TOut, TV, TAng>(v, angle, angleUnit);

    public static Vector2<float> Rotate(Vector2<double> v, double angle, AngleUnit angleUnit = AngleUnit.Degrees)
        => Vector2<float>.Rotate(v, angle, angleUnit);

    public static Vector2<TOut> Rotate<TOut>(Vector2<double> v, double angle, AngleUnit angleUnit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        => Vector2<double>.Rotate<TOut>(v, angle, angleUnit);


    public static Vector2<float> FromPolar<TR, TA>(TR radius, TA angle, AngleUnit angleUnit = AngleUnit.Degrees)
        where TR : INumber<TR>
        where TA : INumber<TA>
        => Vector2<float>.FromPolar(radius, angle, angleUnit);

    public static Vector2<TOut> FromPolar<TOut, TR, TA>(TR radius, TA angle, AngleUnit angleUnit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        where TR : INumber<TR>
        where TA : INumber<TA>
        => Vector2<float>.FromPolar<TOut, TR, TA>(radius, angle, angleUnit);

    public static Vector2<float> FromPolar(double radius, double angle, AngleUnit angleUnit = AngleUnit.Degrees)
        => Vector2<float>.FromPolar(radius, angle, angleUnit);

    public static Vector2<TOut> FromPolar<TOut>(double radius, double angle, AngleUnit angleUnit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        => Vector2<double>.FromPolar<TOut>(radius, angle, angleUnit);


    public static Vector2<float> Normalize<TA>(Vector2<TA> v)
        where TA : unmanaged, INumber<TA>
        => Vector2<float>.Normalize(v);

    public static Vector2<TOut> Normalize<TOut, TA>(Vector2<TA> v)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        => Vector2<float>.Normalize<TOut, TA>(v);

    public static Vector2<float> Normalize(Vector2<double> v)
        => Vector2<float>.Normalize(v);

    public static Vector2<TOut> Normalize<TOut>(Vector2<double> v)
        where TOut : unmanaged, INumber<TOut>
        => Vector2<double>.Normalize<TOut>(v);
    
    public static Vector2<float> Abs<TA>(Vector2<TA> v)
        where TA : unmanaged, INumber<TA>
        => Vector2<float>.Abs(v);
    
    public static Vector2<TOut> Abs<TOut, TA>(Vector2<TA> v)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        => Vector2<float>.Abs<TOut, TA>(v);
    
    public static Vector2<float> Abs(Vector2<double> v)
        => Vector2<float>.Abs(v);
    
    public static Vector2<TOut> Abs<TOut>(Vector2<double> v)
        where TOut : unmanaged, INumber<TOut>
        => Vector2<double>.Abs<TOut>(v);
    
    public static Vector2<float> Sign<TA>(Vector2<TA> v)
        where TA : unmanaged, INumber<TA>
        => Vector2<float>.Sign(v);
    
    public static Vector2<TOut> Sign<TOut, TA>(Vector2<TA> v)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        => Vector2<float>.Sign<TOut, TA>(v);
    
    public static Vector2<float> Sign(Vector2<double> v)
        => Vector2<float>.Sign(v);
    
    public static Vector2<TOut> Sign<TOut>(Vector2<double> v)
        where TOut : unmanaged, INumber<TOut>
        => Vector2<double>.Sign<TOut>(v);
    
#endregion
}