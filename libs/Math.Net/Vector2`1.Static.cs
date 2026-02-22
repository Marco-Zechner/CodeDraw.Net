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
    public static float DistanceSquared<TA, TB>(Vector2<TA> a, Vector2<TB> b)
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

    public static float DistanceSquared(Vector2<double> a, Vector2<double> b) => DistanceSquared<float>(a, b);
    public static TOut DistanceSquared<TOut>(Vector2<double> a, Vector2<double> b)
        where TOut : unmanaged, INumber<TOut>
        => DistanceSquared<TOut, double, double>(a, b);
    
    
    public static float Distance<TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
    {
        var dx = MathG.ToFloat(a.X) - MathG.ToFloat(b.X);
        var dy = MathG.ToFloat(a.Y) - MathG.ToFloat(b.Y);
        return MathG.Sqrt(dx * dx + dy * dy);
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

    public static float Distance(Vector2<double> a, Vector2<double> b) => Distance<float>(a, b);
    public static TOut Distance<TOut>(Vector2<double> a, Vector2<double> b) where TOut : unmanaged, INumber<TOut> => Distance<TOut, double, double>(a, b);

    
    public static float Dot<TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => MathG.ToFloat(a.X) * MathG.ToFloat(b.X) + MathG.ToFloat(a.Y) * MathG.ToFloat(b.Y);
    public static TOut Dot<TOut, TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => MathG.FromDouble<TOut>(MathG.ToDouble(a.X) * MathG.ToDouble(b.X) + MathG.ToDouble(a.Y) * MathG.ToDouble(b.Y));

    public static float Dot(Vector2<double> a, Vector2<double> b) => Dot<float>(a, b);
    public static TOut Dot<TOut>(Vector2<double> a, Vector2<double> b) where TOut : unmanaged, INumber<TOut> => Dot<TOut, double, double>(a, b);


    public static float CrossZ<TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => MathG.ToFloat(a.X) * MathG.ToFloat(b.Y) - MathG.ToFloat(a.Y) * MathG.ToFloat(b.X);
    public static TOut CrossZ<TOut, TA, TB>(Vector2<TA> a, Vector2<TB> b)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        => MathG.FromDouble<TOut>(MathG.ToDouble(a.X) * MathG.ToDouble(b.Y) - MathG.ToDouble(a.Y) * MathG.ToDouble(b.X));

    public static float CrossZ(Vector2<double> a, Vector2<double> b) => CrossZ<float>(a, b);
    public static TOut CrossZ<TOut>(Vector2<double> a, Vector2<double> b) where TOut : unmanaged, INumber<TOut> => CrossZ<TOut, double, double>(a, b);

    public static float AngleBetween<TA, TB>(Vector2<TA> a, Vector2<TB> b, AngleUnit angleUnit = AngleUnit.Degrees)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
    {
        var na = Normalize(a);
        var nb = Normalize(b);
        var dot = na.X * nb.X + na.Y * nb.Y;
        dot = MathG.Max(-1f, MathG.Min(1f, dot));

        return MathG.Acos(dot, angleUnit);
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

    public static float AngleBetween(Vector2<double> a, Vector2<double> b, AngleUnit angleUnit = AngleUnit.Degrees)
        => AngleBetween<float>(a, b, angleUnit);

    public static TOut AngleBetween<TOut>(Vector2<double> a, Vector2<double> b, AngleUnit angleUnit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        => AngleBetween<TOut, double, double>(a, b, angleUnit);

#endregion

#region Returns Vector2<TOut>

    public static Vector2<float> Min<TA, TB>(Vector2<TA> a, Vector2<TB> b)
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
        return FromDouble<TOut>(new Vector2<double>(Math.Min(da.X, db.X), Math.Min(da.Y, db.Y)));
    }

    public static Vector2<float> Min(Vector2<double> a, Vector2<double> b) => Min<float>(a, b);
    public static Vector2<TOut> Min<TOut>(Vector2<double> a, Vector2<double> b)
        where TOut : unmanaged, INumber<TOut>
        => Min<TOut, double, double>(a, b);

    public static Vector2<float> Max<TA, TB>(Vector2<TA> a, Vector2<TB> b)
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
        return FromDouble<TOut>(new Vector2<double>(Math.Max(da.X, db.X), Math.Max(da.Y, db.Y)));
    }

    public static Vector2<float> Max(Vector2<double> a, Vector2<double> b) => Max<float>(a, b);
    public static Vector2<TOut> Max<TOut>(Vector2<double> a, Vector2<double> b)
        where TOut : unmanaged, INumber<TOut>
        => Max<TOut, double, double>(a, b);

    // -----------------------------
    // Clamp (component-wise)
    // -----------------------------
    public static Vector2<float> Clamp<TV, TMin, TMax>(Vector2<TV> v, Vector2<TMin> min, Vector2<TMax> max)
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

        return new Vector2<float>(
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

        return FromDouble<TOut>(new Vector2<double>(
            Math.Min(Math.Max(dv.X, dmin.X), dmax.X),
            Math.Min(Math.Max(dv.Y, dmin.Y), dmax.Y)
        ));
    }

    public static Vector2<float> Clamp(Vector2<double> v, Vector2<double> min, Vector2<double> max) => Clamp<float>(v, min, max);
    public static Vector2<TOut> Clamp<TOut>(Vector2<double> v, Vector2<double> min, Vector2<double> max)
        where TOut : unmanaged, INumber<TOut>
        => Clamp<TOut, double, double, double>(v, min, max);

    // -----------------------------
    // Lerp
    // -----------------------------
    public static Vector2<float> Lerp<TA, TB, TT>(Vector2<TA> a, Vector2<TB> b, TT t)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        where TT : INumber<TT>
    {
        var ax = MathG.ToFloat(a.X);
        var ay = MathG.ToFloat(a.Y);
        var bx = MathG.ToFloat(b.X);
        var by = MathG.ToFloat(b.Y);
        var tt = MathG.ToFloat(t);

        return new Vector2<float>(
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

        return FromDouble<TOut>(new Vector2<double>(
            da.X + (db.X - da.X) * dt,
            da.Y + (db.Y - da.Y) * dt
        ));
    }

    public static Vector2<float> Lerp(Vector2<double> a, Vector2<double> b, double t) => Lerp<float>(a, b, t);
    public static Vector2<TOut> Lerp<TOut>(Vector2<double> a, Vector2<double> b, double t)
        where TOut : unmanaged, INumber<TOut>
        => Lerp<TOut, double, double, double>(a, b, t);

    // -----------------------------
    // Reflect
    // -----------------------------
    public static Vector2<float> Reflect<TV, TN>(Vector2<TV> v, Vector2<TN> normal)
        where TV : unmanaged, INumber<TV>
        where TN : unmanaged, INumber<TN>
    {
        var vx = MathG.ToFloat(v.X);
        var vy = MathG.ToFloat(v.Y);
        var nx = MathG.ToFloat(normal.X);
        var ny = MathG.ToFloat(normal.Y);

        var d = vx * nx + vy * ny;
        return new Vector2<float>(vx - 2f * d * nx, vy - 2f * d * ny);
    }

    public static Vector2<TOut> Reflect<TOut, TV, TN>(Vector2<TV> v, Vector2<TN> normal)
        where TOut : unmanaged, INumber<TOut>
        where TV : unmanaged, INumber<TV>
        where TN : unmanaged, INumber<TN>
    {
        var dv = ToDouble(v);
        var dn = ToDouble(normal);

        var d = dv.X * dn.X + dv.Y * dn.Y;
        return FromDouble<TOut>(new Vector2<double>(
            dv.X - 2.0 * d * dn.X,
            dv.Y - 2.0 * d * dn.Y
        ));
    }

    public static Vector2<float> Reflect(Vector2<double> v, Vector2<double> normal) => Reflect<float>(v, normal);
    public static Vector2<TOut> Reflect<TOut>(Vector2<double> v, Vector2<double> normal)
        where TOut : unmanaged, INumber<TOut>
        => Reflect<TOut, double, double>(v, normal);

    // -----------------------------
    // Perpendicular
    // -----------------------------
    public static Vector2<float> PerpendicularCcw<TA>(Vector2<TA> v)
        where TA : unmanaged, INumber<TA>
        => new(-MathG.ToFloat(v.Y), MathG.ToFloat(v.X));

    public static Vector2<TOut> PerpendicularCcw<TOut, TA>(Vector2<TA> v)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
    {
        var dv = ToDouble(v);
        return FromDouble<TOut>(new Vector2<double>(-dv.Y, dv.X));
    }

    public static Vector2<float> PerpendicularCcw(Vector2<double> v) => PerpendicularCcw<float>(v);
    public static Vector2<TOut> PerpendicularCcw<TOut>(Vector2<double> v)
        where TOut : unmanaged, INumber<TOut>
        => PerpendicularCcw<TOut, double>(v);

    public static Vector2<float> PerpendicularCw<TA>(Vector2<TA> v)
        where TA : unmanaged, INumber<TA>
        => new(MathG.ToFloat(v.Y), -MathG.ToFloat(v.X));

    public static Vector2<TOut> PerpendicularCw<TOut, TA>(Vector2<TA> v)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
    {
        var dv = ToDouble(v);
        return FromDouble<TOut>(new Vector2<double>(dv.Y, -dv.X));
    }

    public static Vector2<float> PerpendicularCw(Vector2<double> v) => PerpendicularCw<float>(v);
    public static Vector2<TOut> PerpendicularCw<TOut>(Vector2<double> v)
        where TOut : unmanaged, INumber<TOut>
        => PerpendicularCw<TOut, double>(v);

    // -----------------------------
    // Rotate
    // -----------------------------
    public static Vector2<float> Rotate<TV, TAng>(Vector2<TV> v, TAng angle, AngleUnit angleUnit = AngleUnit.Degrees)
        where TV : unmanaged, INumber<TV>
        where TAng : INumber<TAng>
    {
        var c = MathG.Cos(angle, angleUnit);
        var s = MathG.Sin(angle, angleUnit);

        var x = MathG.ToFloat(v.X);
        var y = MathG.ToFloat(v.Y);

        return new Vector2<float>(x * c - y * s, x * s + y * c);
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

        return FromDouble<TOut>(new Vector2<double>(x * c - y * s, x * s + y * c));
    }

    public static Vector2<float> Rotate(Vector2<double> v, double angle, AngleUnit angleUnit = AngleUnit.Degrees)
        => Rotate<float>(v, angle, angleUnit);

    public static Vector2<TOut> Rotate<TOut>(Vector2<double> v, double angle, AngleUnit angleUnit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        => Rotate<TOut, double, double>(v, angle, angleUnit);

    // -----------------------------
    // FromPolar
    // -----------------------------
    public static Vector2<float> FromPolar<TR, TA>(TR radius, TA angle, AngleUnit angleUnit = AngleUnit.Degrees)
        where TR : INumber<TR>
        where TA : INumber<TA>
    {
        var r = MathG.ToFloat(radius);
        var a = MathG.ToFloat(angle);
        return new Vector2<float>(r * MathG.Cos(a, angleUnit), r * MathG.Sin(a, angleUnit));
    }

    public static Vector2<TOut> FromPolar<TOut, TR, TA>(TR radius, TA angle, AngleUnit angleUnit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        where TR : INumber<TR>
        where TA : INumber<TA>
    {
        var r = MathG.ToDouble(radius);
        var a = MathG.ToDouble(angle);
        return FromDouble<TOut>(new Vector2<double>(r * MathG.Cos<double>(a, angleUnit), r * MathG.Sin<double>(a, angleUnit)));
    }

    public static Vector2<float> FromPolar(double radius, double angle, AngleUnit angleUnit = AngleUnit.Degrees) => FromPolar<float>(radius, angle, angleUnit);
    public static Vector2<TOut> FromPolar<TOut>(double radius, double angle, AngleUnit angleUnit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        => FromPolar<TOut, double, double>(radius, angle, angleUnit);

    // -----------------------------
    // Normalize
    // -----------------------------
    public static Vector2<float> Normalize<TA>(Vector2<TA> v)
        where TA : unmanaged, INumber<TA>
    {
        var x = MathG.ToFloat(v.X);
        var y = MathG.ToFloat(v.Y);
        var len = MathF.Sqrt(x * x + y * y);
        if (len == 0f) return Vector2<float>.Zero;
        return new Vector2<float>(x / len, y / len);
    }

    public static Vector2<TOut> Normalize<TOut, TA>(Vector2<TA> v)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
    {
        var x = MathG.ToDouble(v.X);
        var y = MathG.ToDouble(v.Y);
        var len = Math.Sqrt(x * x + y * y);
        if (len == 0.0) return Vector2<TOut>.Zero;
        return FromDouble<TOut>(new Vector2<double>(x / len, y / len));
    }

    public static Vector2<float> Normalize(Vector2<double> v) => Normalize<float>(v);
    public static Vector2<TOut> Normalize<TOut>(Vector2<double> v)
        where TOut : unmanaged, INumber<TOut>
        => Normalize<TOut, double>(v);

    public static Vector2<float> Abs<TA>(Vector2<TA> v)
        where TA : unmanaged, INumber<TA>
        => new(MathG.Abs(MathG.ToFloat(v.X)), MathG.Abs(MathG.ToFloat(v.Y)));
    
    public static Vector2<TOut> Abs<TOut, TA>(Vector2<TA> v)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
    {
        var dv = ToDouble(v);
        return FromDouble<TOut>(new Vector2<double>(MathG.Abs(dv.X), MathG.Abs(dv.Y)));
    }
    
    public static Vector2<float> Abs(Vector2<double> v) => Abs<float>(v);
    public static Vector2<TOut> Abs<TOut>(Vector2<double> v)
        where TOut : unmanaged, INumber<TOut>
        => Abs<TOut, double>(v);
    
    public static Vector2<float> Sign<TA>(Vector2<TA> v)
        where TA : unmanaged, INumber<TA>
        => new(MathG.Sign(MathG.ToFloat(v.X)), MathG.Sign(MathG.ToFloat(v.Y)));
    
    public static Vector2<TOut> Sign<TOut, TA>(Vector2<TA> v)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
    {
        var dv = ToDouble(v);
        return FromDouble<TOut>(new Vector2<double>(MathG.Sign(dv.X), MathG.Sign(dv.Y)));
    }
    
    public static Vector2<float> Sign(Vector2<double> v) => Sign<float>(v);
    public static Vector2<TOut> Sign<TOut>(Vector2<double> v)
        where TOut : unmanaged, INumber<TOut>
        => Sign<TOut, double>(v);
    
#endregion
}