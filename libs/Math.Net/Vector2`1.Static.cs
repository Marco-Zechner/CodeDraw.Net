using System.Numerics;

namespace MarcoZechner.MathDotNet;

public readonly partial record struct Vector2<T>
    where T : unmanaged, INumber<T>
{
    internal static Vector2<TOut> FromDouble<TOut>(Vector2<double> v) where TOut : unmanaged, INumber<TOut>
        => new(MathG.FromDouble<TOut>(v.X), MathG.FromDouble<TOut>(v.Y));
    
    internal static Vector2<double> ToDouble<TA>(Vector2<TA> v)
        where TA : unmanaged, INumber<TA>
        => new(MathG.ToDouble(v.X), MathG.ToDouble(v.Y));
    
#region Returns Number
    public static float DistanceSquaredF<TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
    {
        var dx = MathG.ToFloat(a.X) - MathG.ToFloat(b.X);
        var dy = MathG.ToFloat(a.Y) - MathG.ToFloat(b.Y);
        return dx * dx + dy * dy;
    }

    public static TOut DistanceSquared<TOut, TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
    {
        var dx = MathG.ToDouble(a.X) - MathG.ToDouble(b.X);
        var dy = MathG.ToDouble(a.Y) - MathG.ToDouble(b.Y);
        return MathG.FromDouble<TOut>(dx * dx + dy * dy);
    }

    public static float DistanceSquaredF(Vector2<double> a, Vector2<double> b) => DistanceSquaredF<double, double>(a, b);
    public static TOut DistanceSquared<TOut>(Vector2<double> a, Vector2<double> b)
        where TOut : unmanaged, INumber<TOut>
        => DistanceSquared<TOut, double, double>(a, b);
    
    
    public static float DistanceF<TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
    {
        var dx = MathG.ToFloat(a.X) - MathG.ToFloat(b.X);
        var dy = MathG.ToFloat(a.Y) - MathG.ToFloat(b.Y);
        return MathF.Sqrt(dx * dx + dy * dy);
    }
    public static TOut Distance<TOut, TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
    {
        var dx = MathG.ToDouble(a.X) - MathG.ToDouble(b.X);
        var dy = MathG.ToDouble(a.Y) - MathG.ToDouble(b.Y);
        return MathG.FromDouble<TOut>(Math.Sqrt(dx * dx + dy * dy));
    }

    public static float DistanceF(Vector2<double> a, Vector2<double> b) => DistanceF<double, double>(a, b);
    public static TOut Distance<TOut>(Vector2<double> a, Vector2<double> b) where TOut : unmanaged, INumber<TOut> => Distance<TOut, double, double>(a, b);

    
    public static float DotF<TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => MathG.ToFloat(a.X) * MathG.ToFloat(b.X) + MathG.ToFloat(a.Y) * MathG.ToFloat(b.Y);
    public static TOut Dot<TOut, TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => MathG.FromDouble<TOut>(MathG.ToDouble(a.X) * MathG.ToDouble(b.X) + MathG.ToDouble(a.Y) * MathG.ToDouble(b.Y));

    public static float DotF(Vector2<double> a, Vector2<double> b) => DotF<double, double>(a, b);
    public static TOut Dot<TOut>(Vector2<double> a, Vector2<double> b) where TOut : unmanaged, INumber<TOut> => Dot<TOut, double, double>(a, b);


    public static float CrossZF<TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => MathG.ToFloat(a.X) * MathG.ToFloat(b.Y) - MathG.ToFloat(a.Y) * MathG.ToFloat(b.X);
    public static TOut CrossZ<TOut, TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => MathG.FromDouble<TOut>(MathG.ToDouble(a.X) * MathG.ToDouble(b.Y) - MathG.ToDouble(a.Y) * MathG.ToDouble(b.X));

    public static float CrossZF(Vector2<double> a, Vector2<double> b) => CrossZF<double, double>(a, b);
    public static TOut CrossZ<TOut>(Vector2<double> a, Vector2<double> b) where TOut : unmanaged, INumber<TOut> => CrossZ<TOut, double, double>(a, b);

    public static float AngleBetweenF<TA, TB>(Vector2<TA> a, Vector2<TB> b, AngleUnit angleUnit = AngleUnit.Degrees)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
    {
        var na = NormalizeF(a);
        var nb = NormalizeF(b);
        var dot = na.X * nb.X + na.Y * nb.Y;
        dot = MathG.Max(-1f, MathG.Min(1f, dot));

        return MathG.AcosF(dot, angleUnit);
    }

    public static TOut AngleBetween<TOut, TA, TB>(Vector2<TA> a, Vector2<TB> b, AngleUnit angleUnit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
    {
        var na = Normalize<double, TA>(a);
        var nb = Normalize<double, TB>(b);

        var dot = na.X * nb.X + na.Y * nb.Y;
        dot = MathG.Max(-1.0, MathG.Min(1.0, dot));

        return MathG.Acos<TOut>(dot, angleUnit);
    }

    public static float AngleBetweenF(Vector2<double> a, Vector2<double> b, AngleUnit angleUnit = AngleUnit.Degrees)
        => AngleBetweenF<double, double>(a, b, angleUnit);

    public static TOut AngleBetween<TOut>(Vector2<double> a, Vector2<double> b, AngleUnit angleUnit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        => AngleBetween<TOut, double, double>(a, b, angleUnit);

#endregion

#region Returns Vector2<TOut>

    public static Vector2<float> MinF<TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => new(MathF.Min(MathG.ToFloat(a.X), MathG.ToFloat(b.X)),
               MathF.Min(MathG.ToFloat(a.Y), MathG.ToFloat(b.Y)));

    public static Vector2<TOut> Min<TOut, TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
    {
        var da = ToDouble(a);
        var db = ToDouble(b);
        return FromDouble<TOut>(new(Math.Min(da.X, db.X), Math.Min(da.Y, db.Y)));
    }

    public static Vector2<float> MinF(Vector2<double> a, Vector2<double> b) => MinF<double, double>(a, b);
    public static Vector2<TOut> Min<TOut>(Vector2<double> a, Vector2<double> b)
        where TOut : unmanaged, INumber<TOut>
        => Min<TOut, double, double>(a, b);

    public static Vector2<float> MaxF<TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => new(MathF.Max(MathG.ToFloat(a.X), MathG.ToFloat(b.X)),
               MathF.Max(MathG.ToFloat(a.Y), MathG.ToFloat(b.Y)));

    public static Vector2<TOut> Max<TOut, TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
    {
        var da = ToDouble(a);
        var db = ToDouble(b);
        return FromDouble<TOut>(new(Math.Max(da.X, db.X), Math.Max(da.Y, db.Y)));
    }

    public static Vector2<float> MaxF(Vector2<double> a, Vector2<double> b) => MaxF<double, double>(a, b);
    public static Vector2<TOut> Max<TOut>(Vector2<double> a, Vector2<double> b)
        where TOut : unmanaged, INumber<TOut>
        => Max<TOut, double, double>(a, b);

    // -----------------------------
    // Clamp (component-wise)
    // -----------------------------
    public static Vector2<float> ClampF<TV, TMin, TMax>(Vector2<TV> v, Vector2<TMin> min, Vector2<TMax> max)
        where TV : unmanaged, INumber<TV>
        where TMin : unmanaged, INumber<TMin>
        where TMax : unmanaged, INumber<TMax>
    {
        var x = MathG.ToFloat(v.X);
        var y = MathG.ToFloat(v.Y);
        var minX = MathG.ToFloat(min.X);
        var minY = MathG.ToFloat(min.Y);
        var maxX = MathG.ToFloat(max.X);
        var maxY = MathG.ToFloat(max.Y);

        return new(
            MathF.Min(MathF.Max(x, minX), maxX),
            MathF.Min(MathF.Max(y, minY), maxY)
        );
    }

    public static Vector2<TOut> Clamp<TOut, TV, TMin, TMax>(Vector2<TV> v, Vector2<TMin> min, Vector2<TMax> max)
        where TOut : unmanaged, INumber<TOut>
        where TV : unmanaged, INumber<TV>
        where TMin : unmanaged, INumber<TMin>
        where TMax : unmanaged, INumber<TMax>
    {
        var dv = ToDouble(v);
        var dmin = ToDouble(min);
        var dmax = ToDouble(max);

        return FromDouble<TOut>(new(
            Math.Min(Math.Max(dv.X, dmin.X), dmax.X),
            Math.Min(Math.Max(dv.Y, dmin.Y), dmax.Y)
        ));
    }

    public static Vector2<float> ClampF(Vector2<double> v, Vector2<double> min, Vector2<double> max) => ClampF<double, double, double>(v, min, max);
    public static Vector2<TOut> Clamp<TOut>(Vector2<double> v, Vector2<double> min, Vector2<double> max)
        where TOut : unmanaged, INumber<TOut>
        => Clamp<TOut, double, double, double>(v, min, max);

    // -----------------------------
    // Lerp
    // -----------------------------
    public static Vector2<float> LerpF<TA, TB, TT>(Vector2<TA> a, Vector2<TB> b, TT t)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        where TT : INumber<TT>
    {
        var ax = MathG.ToFloat(a.X);
        var ay = MathG.ToFloat(a.Y);
        var bx = MathG.ToFloat(b.X);
        var by = MathG.ToFloat(b.Y);
        var tt = MathG.ToFloat(t);

        return new(
            ax + (bx - ax) * tt,
            ay + (by - ay) * tt
        );
    }

    public static Vector2<TOut> Lerp<TOut, TA, TB, TT>(Vector2<TA> a, Vector2<TB> b, TT t)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        where TT : INumber<TT>
    {
        var da = ToDouble(a);
        var db = ToDouble(b);
        var dt = MathG.ToDouble(t);

        return FromDouble<TOut>(new(
            da.X + (db.X - da.X) * dt,
            da.Y + (db.Y - da.Y) * dt
        ));
    }

    public static Vector2<float> LerpF(Vector2<double> a, Vector2<double> b, double t) => LerpF<double, double, double>(a, b, t);
    public static Vector2<TOut> Lerp<TOut>(Vector2<double> a, Vector2<double> b, double t)
        where TOut : unmanaged, INumber<TOut>
        => Lerp<TOut, double, double, double>(a, b, t);

    // -----------------------------
    // Reflect
    // -----------------------------
    public static Vector2<float> ReflectF<TV, TN>(Vector2<TV> v, Vector2<TN> normal)
        where TV : unmanaged, INumber<TV>
        where TN : unmanaged, INumber<TN>
    {
        var vx = MathG.ToFloat(v.X);
        var vy = MathG.ToFloat(v.Y);
        var nx = MathG.ToFloat(normal.X);
        var ny = MathG.ToFloat(normal.Y);

        var d = vx * nx + vy * ny;
        return new(vx - 2f * d * nx, vy - 2f * d * ny);
    }

    public static Vector2<TOut> Reflect<TOut, TV, TN>(Vector2<TV> v, Vector2<TN> normal)
        where TOut : unmanaged, INumber<TOut>
        where TV : unmanaged, INumber<TV>
        where TN : unmanaged, INumber<TN>
    {
        var dv = ToDouble(v);
        var dn = ToDouble(normal);

        var d = dv.X * dn.X + dv.Y * dn.Y;
        return FromDouble<TOut>(new(
            dv.X - 2.0 * d * dn.X,
            dv.Y - 2.0 * d * dn.Y
        ));
    }

    public static Vector2<float> ReflectF(Vector2<double> v, Vector2<double> normal) => ReflectF<double, double>(v, normal);
    public static Vector2<TOut> Reflect<TOut>(Vector2<double> v, Vector2<double> normal)
        where TOut : unmanaged, INumber<TOut>
        => Reflect<TOut, double, double>(v, normal);

    // -----------------------------
    // Perpendicular
    // -----------------------------
    public static Vector2<float> PerpendicularCcwF<TA>(Vector2<TA> v)
        where TA : unmanaged, INumber<TA>
        => new(-MathG.ToFloat(v.Y), MathG.ToFloat(v.X));

    public static Vector2<TOut> PerpendicularCcw<TOut, TA>(Vector2<TA> v)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
    {
        var dv = ToDouble(v);
        return FromDouble<TOut>(new(-dv.Y, dv.X));
    }

    public static Vector2<float> PerpendicularCcwF(Vector2<double> v) => PerpendicularCcwF<double>(v);
    public static Vector2<TOut> PerpendicularCcw<TOut>(Vector2<double> v)
        where TOut : unmanaged, INumber<TOut>
        => PerpendicularCcw<TOut, double>(v);

    public static Vector2<float> PerpendicularCwF<TA>(Vector2<TA> v)
        where TA : unmanaged, INumber<TA>
        => new(MathG.ToFloat(v.Y), -MathG.ToFloat(v.X));

    public static Vector2<TOut> PerpendicularCw<TOut, TA>(Vector2<TA> v)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
    {
        var dv = ToDouble(v);
        return FromDouble<TOut>(new(dv.Y, -dv.X));
    }

    public static Vector2<float> PerpendicularCwF(Vector2<double> v) => PerpendicularCwF<double>(v);
    public static Vector2<TOut> PerpendicularCw<TOut>(Vector2<double> v)
        where TOut : unmanaged, INumber<TOut>
        => PerpendicularCw<TOut, double>(v);

    // -----------------------------
    // Rotate
    // -----------------------------
    public static Vector2<float> RotateF<TV, TAng>(Vector2<TV> v, TAng angle, AngleUnit angleUnit = AngleUnit.Degrees)
        where TV : unmanaged, INumber<TV>
        where TAng : INumber<TAng>
    {
        var c = MathG.CosF(angle, angleUnit);
        var s = MathG.SinF(angle, angleUnit);

        var x = MathG.ToFloat(v.X);
        var y = MathG.ToFloat(v.Y);

        return new(x * c - y * s, x * s + y * c);
    }

    public static Vector2<TOut> Rotate<TOut, TV, TAng>(Vector2<TV> v, TAng angle, AngleUnit angleUnit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        where TV : unmanaged, INumber<TV>
        where TAng : INumber<TAng>
    {
        var da = MathG.ToDouble(angle);
        
        var c = MathG.Cos<double>(da, angleUnit);
        var s = MathG.Sin<double>(da, angleUnit);

        var x = MathG.ToDouble(v.X);
        var y = MathG.ToDouble(v.Y);

        return FromDouble<TOut>(new(x * c - y * s, x * s + y * c));
    }

    public static Vector2<float> RotateF(Vector2<double> v, double angle, AngleUnit angleUnit = AngleUnit.Degrees)
        => RotateF<double, double>(v, angle, angleUnit);

    public static Vector2<TOut> Rotate<TOut>(Vector2<double> v, double angle, AngleUnit angleUnit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        => Rotate<TOut, double, double>(v, angle, angleUnit);

    // -----------------------------
    // FromPolar
    // -----------------------------
    public static Vector2<float> FromPolarF<TR, TA>(TR radius, TA angle, AngleUnit angleUnit = AngleUnit.Degrees)
        where TR : INumber<TR>
        where TA : INumber<TA>
    {
        var r = MathG.ToFloat(radius);
        var a = MathG.ToFloat(angle);
        return new(r * MathG.CosF(a, angleUnit), r * MathG.SinF(a, angleUnit));
    }

    public static Vector2<TOut> FromPolar<TOut, TR, TA>(TR radius, TA angle, AngleUnit angleUnit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        where TR : INumber<TR>
        where TA : INumber<TA>
    {
        var r = MathG.ToDouble(radius);
        var a = MathG.ToDouble(angle);
        return FromDouble<TOut>(new(r * MathG.Cos<double>(a, angleUnit), r * MathG.Sin<double>(a, angleUnit)));
    }

    public static Vector2<float> FromPolarF(double radius, double angle, AngleUnit angleUnit = AngleUnit.Degrees) => FromPolarF<double, double>(radius, angle, angleUnit);
    public static Vector2<TOut> FromPolar<TOut>(double radius, double angle, AngleUnit angleUnit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        => FromPolar<TOut, double, double>(radius, angle, angleUnit);

    // -----------------------------
    // Normalize
    // -----------------------------
    public static Vector2<float> NormalizeF<TA>(Vector2<TA> v)
        where TA : unmanaged, INumber<TA>
    {
        var x = MathG.ToFloat(v.X);
        var y = MathG.ToFloat(v.Y);
        var len = MathF.Sqrt(x * x + y * y);
        if (len == 0f) return Vector2<float>.Zero;
        return new(x / len, y / len);
    }

    public static Vector2<TOut> Normalize<TOut, TA>(Vector2<TA> v)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
    {
        var x = MathG.ToDouble(v.X);
        var y = MathG.ToDouble(v.Y);
        var len = Math.Sqrt(x * x + y * y);
        if (len == 0.0) return Vector2<TOut>.Zero;
        return FromDouble<TOut>(new(x / len, y / len));
    }

    public static Vector2<float> NormalizeF(Vector2<double> v) => NormalizeF<double>(v);
    public static Vector2<TOut> Normalize<TOut>(Vector2<double> v)
        where TOut : unmanaged, INumber<TOut>
        => Normalize<TOut, double>(v);

#endregion
}