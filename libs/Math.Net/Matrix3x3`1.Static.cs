using System.Numerics;

namespace MarcoZechner.MathDotNet;

public readonly partial record struct Matrix3x3<T>
    where T : unmanaged, INumber<T>
{
    internal static Matrix3x3<TOut> FromDouble<TOut>(Matrix3x3<double> m)
        where TOut : unmanaged, INumber<TOut>
        => new(
            MathG.FromDouble<TOut>(m.M11), MathG.FromDouble<TOut>(m.M12), MathG.FromDouble<TOut>(m.M13),
            MathG.FromDouble<TOut>(m.M21), MathG.FromDouble<TOut>(m.M22), MathG.FromDouble<TOut>(m.M23),
            MathG.FromDouble<TOut>(m.M31), MathG.FromDouble<TOut>(m.M32), MathG.FromDouble<TOut>(m.M33)
        );

    internal static Matrix3x3<double> ToDouble<TA>(Matrix3x3<TA> m)
        where TA : unmanaged, INumber<TA>
        => new(
            MathG.ToDouble(m.M11), MathG.ToDouble(m.M12), MathG.ToDouble(m.M13),
            MathG.ToDouble(m.M21), MathG.ToDouble(m.M22), MathG.ToDouble(m.M23),
            MathG.ToDouble(m.M31), MathG.ToDouble(m.M32), MathG.ToDouble(m.M33)
        );

#region Returns Number

    public static float DeterminantF<TA>(Matrix3x3<TA> m)
        where TA : unmanaged, INumber<TA>
    {
        var d = ToDouble(m);
        var det =
            d.M11 * (d.M22 * d.M33 - d.M23 * d.M32) -
            d.M12 * (d.M21 * d.M33 - d.M23 * d.M31) +
            d.M13 * (d.M21 * d.M32 - d.M22 * d.M31);
        return (float)det;
    }

    public static TOut Determinant<TOut, TA>(Matrix3x3<TA> m)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
    {
        var d = ToDouble(m);
        var det =
            d.M11 * (d.M22 * d.M33 - d.M23 * d.M32) -
            d.M12 * (d.M21 * d.M33 - d.M23 * d.M31) +
            d.M13 * (d.M21 * d.M32 - d.M22 * d.M31);
        return MathG.FromDouble<TOut>(det);
    }

    public static float DeterminantF(Matrix3x3<double> m) => DeterminantF<double>(m);

    public static TOut Determinant<TOut>(Matrix3x3<double> m)
        where TOut : unmanaged, INumber<TOut>
        => Determinant<TOut, double>(m);


    public static float TraceF<TA>(Matrix3x3<TA> m)
        where TA : unmanaged, INumber<TA>
        => MathG.ToFloat(m.M11) + MathG.ToFloat(m.M22) + MathG.ToFloat(m.M33);

    public static TOut Trace<TOut, TA>(Matrix3x3<TA> m)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        => MathG.FromDouble<TOut>(MathG.ToDouble(m.M11) + MathG.ToDouble(m.M22) + MathG.ToDouble(m.M33));

    public static float TraceF(Matrix3x3<double> m) => TraceF<double>(m);

    public static TOut Trace<TOut>(Matrix3x3<double> m)
        where TOut : unmanaged, INumber<TOut>
        => Trace<TOut, double>(m);

#endregion

#region Returns Matrix3X3<TOut>

    // -----------------------------
    // CreateRotation
    // -----------------------------

    public static Matrix3x3<float> CreateRotationF<TAng>(TAng angle, AngleUnit unit = AngleUnit.Degrees)
        where TAng : INumber<TAng>
    {
        var c = MathG.CosF(angle, unit);
        var s = MathG.SinF(angle, unit);
        return new(
            c, -s, 0f,
            s,  c, 0f,
            0f, 0f, 1f
        );
    }

    public static Matrix3x3<TOut> CreateRotation<TOut, TAng>(TAng angle, AngleUnit unit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        where TAng : INumber<TAng>
    {
        var da = MathG.ToDouble(angle);
        var c = MathG.Cos<double>(da, unit);
        var s = MathG.Sin<double>(da, unit);
        return FromDouble<TOut>(new(
            c, -s, 0.0,
            s,  c, 0.0,
            0.0, 0.0, 1.0
        ));
    }

    public static Matrix3x3<float> CreateRotationF(double angle, AngleUnit unit = AngleUnit.Degrees)
        => CreateRotationF<double>(angle, unit);

    public static Matrix3x3<TOut> CreateRotation<TOut>(double angle, AngleUnit unit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        => CreateRotation<TOut, double>(angle, unit);

    // -----------------------------
    // CreateTranslation
    // -----------------------------
    public static Matrix3x3<float> CreateTranslationF<TTx, TTy>(TTx tx, TTy ty)
        where TTx : INumber<TTx>
        where TTy : INumber<TTy>
        => new(
            1f, 0f, MathG.ToFloat(tx),
            0f, 1f, MathG.ToFloat(ty),
            0f, 0f, 1f
        );

    public static Matrix3x3<TOut> CreateTranslation<TOut, TTx, TTy>(TTx tx, TTy ty)
        where TOut : unmanaged, INumber<TOut>
        where TTx : INumber<TTx>
        where TTy : INumber<TTy>
        => FromDouble<TOut>(new(
            1.0, 0.0, MathG.ToDouble(tx),
            0.0, 1.0, MathG.ToDouble(ty),
            0.0, 0.0, 1.0
        ));

    public static Matrix3x3<float> CreateTranslationF(double tx, double ty)
        => CreateTranslationF<double, double>(tx, ty);

    public static Matrix3x3<TOut> CreateTranslation<TOut>(double tx, double ty)
        where TOut : unmanaged, INumber<TOut>
        => CreateTranslation<TOut, double, double>(tx, ty);


    // -----------------------------
    // CreateScale
    // -----------------------------
    public static Matrix3x3<float> CreateScaleF<TSx, TSy>(TSx sx, TSy sy)
        where TSx : INumber<TSx>
        where TSy : INumber<TSy>
        => new(
            MathG.ToFloat(sx), 0f,             0f,
            0f,             MathG.ToFloat(sy), 0f,
            0f,             0f,             1f
        );

    public static Matrix3x3<TOut> CreateScale<TOut, TSx, TSy>(TSx sx, TSy sy)
        where TOut : unmanaged, INumber<TOut>
        where TSx : INumber<TSx>
        where TSy : INumber<TSy>
        => FromDouble<TOut>(new(
            MathG.ToDouble(sx), 0.0,              0.0,
            0.0,              MathG.ToDouble(sy), 0.0,
            0.0,              0.0,              1.0
        ));

    public static Matrix3x3<float> CreateScaleF(double sx, double sy)
        => CreateScaleF<double, double>(sx, sy);

    public static Matrix3x3<TOut> CreateScale<TOut>(double sx, double sy)
        where TOut : unmanaged, INumber<TOut>
        => CreateScale<TOut, double, double>(sx, sy);

    // -----------------------------
    // CreateBasis
    // -----------------------------

    public static Matrix3x3<float> CreateBasisF<TX, TY, TT>(Vector2<TX> xAxis, Vector2<TY> yAxis, Vector2<TT> translation)
        where TX : unmanaged, INumber<TX>
        where TY : unmanaged, INumber<TY>
        where TT : unmanaged, INumber<TT>
        => new(
            MathG.ToFloat(xAxis.X), MathG.ToFloat(yAxis.X), MathG.ToFloat(translation.X),
            MathG.ToFloat(xAxis.Y), MathG.ToFloat(yAxis.Y), MathG.ToFloat(translation.Y),
            0f,                 0f,                 1f
        );

    public static Matrix3x3<TOut> CreateBasis<TOut, TX, TY, TT>(Vector2<TX> xAxis, Vector2<TY> yAxis, Vector2<TT> translation)
        where TOut : unmanaged, INumber<TOut>
        where TX : unmanaged, INumber<TX>
        where TY : unmanaged, INumber<TY>
        where TT : unmanaged, INumber<TT>
        => FromDouble<TOut>(new(
            MathG.ToDouble(xAxis.X), MathG.ToDouble(yAxis.X), MathG.ToDouble(translation.X),
            MathG.ToDouble(xAxis.Y), MathG.ToDouble(yAxis.Y), MathG.ToDouble(translation.Y),
            0.0,                 0.0,                 1.0
        ));

    public static Matrix3x3<float> CreateBasisF(Vector2<double> xAxis, Vector2<double> yAxis, Vector2<double> translation)
        => CreateBasisF<double, double, double>(xAxis, yAxis, translation);

    public static Matrix3x3<TOut> CreateBasis<TOut>(Vector2<double> xAxis, Vector2<double> yAxis, Vector2<double> translation)
        where TOut : unmanaged, INumber<TOut>
        => CreateBasis<TOut, double, double, double>(xAxis, yAxis, translation);

    // -----------------------------
    // Shear
    // -----------------------------
    
    public static Matrix3x3<float> CreateShearF<TShx, TShy>(TShx shx, TShy shy)
        where TShx : INumber<TShx>
        where TShy : INumber<TShy>
        => new(
            1f, MathG.ToFloat(shx), 0f,
            MathG.ToFloat(shy), 1f, 0f,
            0f, 0f, 1f
        );
    
    public static Matrix3x3<TOut> CreateShear<TOut, TShx, TShy>(TShx shx, TShy shy)
        where TOut : unmanaged, INumber<TOut>
        where TShx : INumber<TShx>
        where TShy : INumber<TShy>
        => FromDouble<TOut>(new(
            1.0, MathG.ToDouble(shx), 0.0,
            MathG.ToDouble(shy), 1.0, 0.0,
            0.0, 0.0, 1.0
        ));
    
    public static Matrix3x3<float> CreateShearF(double shx, double shy)
        => CreateShearF<double, double>(shx, shy);
    
    public static Matrix3x3<TOut> CreateShear<TOut>(double shx, double shy)
        where TOut : unmanaged, INumber<TOut>
        => CreateShear<TOut, double, double>(shx, shy);

    // -----------------------------
    // Lerp
    // -----------------------------

    public static Matrix3x3<float> LerpF<TA, TB, TT>(Matrix3x3<TA> a, Matrix3x3<TB> b, TT t)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        where TT : INumber<TT>
    {
        var tt = MathG.ToFloat(t);
        return new(
            MathG.ToFloat(a.M11) + (MathG.ToFloat(b.M11) - MathG.ToFloat(a.M11)) * tt,
            MathG.ToFloat(a.M12) + (MathG.ToFloat(b.M12) - MathG.ToFloat(a.M12)) * tt,
            MathG.ToFloat(a.M13) + (MathG.ToFloat(b.M13) - MathG.ToFloat(a.M13)) * tt,

            MathG.ToFloat(a.M21) + (MathG.ToFloat(b.M21) - MathG.ToFloat(a.M21)) * tt,
            MathG.ToFloat(a.M22) + (MathG.ToFloat(b.M22) - MathG.ToFloat(a.M22)) * tt,
            MathG.ToFloat(a.M23) + (MathG.ToFloat(b.M23) - MathG.ToFloat(a.M23)) * tt,

            MathG.ToFloat(a.M31) + (MathG.ToFloat(b.M31) - MathG.ToFloat(a.M31)) * tt,
            MathG.ToFloat(a.M32) + (MathG.ToFloat(b.M32) - MathG.ToFloat(a.M32)) * tt,
            MathG.ToFloat(a.M33) + (MathG.ToFloat(b.M33) - MathG.ToFloat(a.M33)) * tt
        );
    }

    public static Matrix3x3<TOut> Lerp<TOut, TA, TB, TT>(Matrix3x3<TA> a, Matrix3x3<TB> b, TT t)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        where TT : INumber<TT>
    {
        var da = ToDouble(a);
        var db = ToDouble(b);
        var tt = MathG.ToDouble(t);
        return FromDouble<TOut>(new(
            da.M11 + (db.M11 - da.M11) * tt,
            da.M12 + (db.M12 - da.M12) * tt,
            da.M13 + (db.M13 - da.M13) * tt,

            da.M21 + (db.M21 - da.M21) * tt,
            da.M22 + (db.M22 - da.M22) * tt,
            da.M23 + (db.M23 - da.M23) * tt,

            da.M31 + (db.M31 - da.M31) * tt,
            da.M32 + (db.M32 - da.M32) * tt,
            da.M33 + (db.M33 - da.M33) * tt
        ));
    }

    public static Matrix3x3<float> LerpF(Matrix3x3<double> a, Matrix3x3<double> b, double t)
        => LerpF<double, double, double>(a, b, t);

    public static Matrix3x3<TOut> Lerp<TOut>(Matrix3x3<double> a, Matrix3x3<double> b, double t)
        where TOut : unmanaged, INumber<TOut>
        => Lerp<TOut, double, double, double>(a, b, t);


    // -----------------------------
    // Transpose
    // -----------------------------

    public static Matrix3x3<float> TransposeF<TA>(Matrix3x3<TA> m)
        where TA : unmanaged, INumber<TA>
        => new(
            MathG.ToFloat(m.M11), MathG.ToFloat(m.M21), MathG.ToFloat(m.M31),
            MathG.ToFloat(m.M12), MathG.ToFloat(m.M22), MathG.ToFloat(m.M32),
            MathG.ToFloat(m.M13), MathG.ToFloat(m.M23), MathG.ToFloat(m.M33)
        );

    public static Matrix3x3<TOut> Transpose<TOut, TA>(Matrix3x3<TA> m)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
    {
        var d = ToDouble(m);
        return FromDouble<TOut>(new(
            d.M11, d.M21, d.M31,
            d.M12, d.M22, d.M32,
            d.M13, d.M23, d.M33
        ));
    }

    public static Matrix3x3<float> TransposeF(Matrix3x3<double> m) => TransposeF<double>(m);

    public static Matrix3x3<TOut> Transpose<TOut>(Matrix3x3<double> m)
        where TOut : unmanaged, INumber<TOut>
        => Transpose<TOut, double>(m);


    // -----------------------------
    // Invert / TryInvert (computed in double, returned as requested)
    // -----------------------------

    public static bool TryInvertF<TA>(Matrix3x3<TA> m, out Matrix3x3<float> inv)
        where TA : unmanaged, INumber<TA>
    {
        var d = ToDouble(m);
        var det = Determinant<double, double>(d);

        if (det == 0.0)
        {
            inv = Matrix3x3<float>.Identity;
            return false;
        }

        var invDet = 1.0 / det;

        var i11 =  (d.M22 * d.M33 - d.M23 * d.M32) * invDet;
        var i12 = -(d.M12 * d.M33 - d.M13 * d.M32) * invDet;
        var i13 =  (d.M12 * d.M23 - d.M13 * d.M22) * invDet;

        var i21 = -(d.M21 * d.M33 - d.M23 * d.M31) * invDet;
        var i22 =  (d.M11 * d.M33 - d.M13 * d.M31) * invDet;
        var i23 = -(d.M11 * d.M23 - d.M13 * d.M21) * invDet;

        var i31 =  (d.M21 * d.M32 - d.M22 * d.M31) * invDet;
        var i32 = -(d.M11 * d.M32 - d.M12 * d.M31) * invDet;
        var i33 =  (d.M11 * d.M22 - d.M12 * d.M21) * invDet;

        inv = FromDouble<float>(new(
            i11, i12, i13,
            i21, i22, i23,
            i31, i32, i33
        ));
        return true;
    }

    public static bool TryInvert<TOut, TA>(Matrix3x3<TA> m, out Matrix3x3<TOut> inv)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
    {
        var d = ToDouble(m);
        var det = Determinant<double, double>(d);

        if (det == 0.0)
        {
            inv = Matrix3x3<TOut>.Identity;
            return false;
        }

        var invDet = 1.0 / det;

        var i11 =  (d.M22 * d.M33 - d.M23 * d.M32) * invDet;
        var i12 = -(d.M12 * d.M33 - d.M13 * d.M32) * invDet;
        var i13 =  (d.M12 * d.M23 - d.M13 * d.M22) * invDet;

        var i21 = -(d.M21 * d.M33 - d.M23 * d.M31) * invDet;
        var i22 =  (d.M11 * d.M33 - d.M13 * d.M31) * invDet;
        var i23 = -(d.M11 * d.M23 - d.M13 * d.M21) * invDet;

        var i31 =  (d.M21 * d.M32 - d.M22 * d.M31) * invDet;
        var i32 = -(d.M11 * d.M32 - d.M12 * d.M31) * invDet;
        var i33 =  (d.M11 * d.M22 - d.M12 * d.M21) * invDet;

        inv = FromDouble<TOut>(new(
            i11, i12, i13,
            i21, i22, i23,
            i31, i32, i33
        ));
        return true;
    }

    public static Matrix3x3<float> InvertF<TA>(Matrix3x3<TA> m)
        where TA : unmanaged, INumber<TA>
    {
        if (!TryInvertF(m, out var inv))
            throw new InvalidOperationException("Matrix is not invertible.");
        return inv;
    }

    public static Matrix3x3<TOut> Invert<TOut, TA>(Matrix3x3<TA> m)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
    {
        if (!TryInvert<TOut, TA>(m, out var inv))
            throw new InvalidOperationException("Matrix is not invertible.");
        return inv;
    }

    public static bool TryInvertF(Matrix3x3<double> m, out Matrix3x3<float> inv) => TryInvertF<double>(m, out inv);

    public static bool TryInvert<TOut>(Matrix3x3<double> m, out Matrix3x3<TOut> inv)
        where TOut : unmanaged, INumber<TOut>
        => TryInvert<TOut, double>(m, out inv);

    public static Matrix3x3<float> InvertF(Matrix3x3<double> m) => InvertF<double>(m);

    public static Matrix3x3<TOut> Invert<TOut>(Matrix3x3<double> m)
        where TOut : unmanaged, INumber<TOut>
        => Invert<TOut, double>(m);


    // -----------------------------
    // TransformAffine / TransformProjective (cross-type)
    // -----------------------------

    public static Vector2<float> TransformAffineF<TM, TV>(Matrix3x3<TM> m, Vector2<TV> v)
        where TM : unmanaged, INumber<TM>
        where TV : unmanaged, INumber<TV>
    {
        var x = MathG.ToFloat(m.M11) * MathG.ToFloat(v.X) + MathG.ToFloat(m.M12) * MathG.ToFloat(v.Y) + MathG.ToFloat(m.M13);
        var y = MathG.ToFloat(m.M21) * MathG.ToFloat(v.X) + MathG.ToFloat(m.M22) * MathG.ToFloat(v.Y) + MathG.ToFloat(m.M23);
        return new(x, y);
    }

    public static Vector2<TOut> TransformAffine<TOut, TM, TV>(Matrix3x3<TM> m, Vector2<TV> v)
        where TOut : unmanaged, INumber<TOut>
        where TM : unmanaged, INumber<TM>
        where TV : unmanaged, INumber<TV>
    {
        var dm = ToDouble(m);
        var dx = MathG.ToDouble(v.X);
        var dy = MathG.ToDouble(v.Y);

        var x = dm.M11 * dx + dm.M12 * dy + dm.M13;
        var y = dm.M21 * dx + dm.M22 * dy + dm.M23;
        return new(MathG.FromDouble<TOut>(x), MathG.FromDouble<TOut>(y));
    }

    public static Vector2<float> TransformAffineF(Matrix3x3<double> m, Vector2<double> v) => TransformAffineF<double, double>(m, v);

    public static Vector2<TOut> TransformAffine<TOut>(Matrix3x3<double> m, Vector2<double> v)
        where TOut : unmanaged, INumber<TOut>
        => TransformAffine<TOut, double, double>(m, v);


    public static Vector2<float> TransformProjectiveF<TM, TV>(Matrix3x3<TM> m, Vector2<TV> v)
        where TM : unmanaged, INumber<TM>
        where TV : unmanaged, INumber<TV>
    {
        var x = MathG.ToFloat(m.M11) * MathG.ToFloat(v.X) + MathG.ToFloat(m.M12) * MathG.ToFloat(v.Y) + MathG.ToFloat(m.M13);
        var y = MathG.ToFloat(m.M21) * MathG.ToFloat(v.X) + MathG.ToFloat(m.M22) * MathG.ToFloat(v.Y) + MathG.ToFloat(m.M23);
        var w = MathG.ToFloat(m.M31) * MathG.ToFloat(v.X) + MathG.ToFloat(m.M32) * MathG.ToFloat(v.Y) + MathG.ToFloat(m.M33);

        if (w == 0f)
            throw new DivideByZeroException("Projective transform produced w=0.");

        return new(x / w, y / w);
    }

    public static Vector2<TOut> TransformProjective<TOut, TM, TV>(Matrix3x3<TM> m, Vector2<TV> v)
        where TOut : unmanaged, INumber<TOut>
        where TM : unmanaged, INumber<TM>
        where TV : unmanaged, INumber<TV>
    {
        var dm = ToDouble(m);
        var dx = MathG.ToDouble(v.X);
        var dy = MathG.ToDouble(v.Y);

        var x = dm.M11 * dx + dm.M12 * dy + dm.M13;
        var y = dm.M21 * dx + dm.M22 * dy + dm.M23;
        var w = dm.M31 * dx + dm.M32 * dy + dm.M33;

        if (w == 0.0)
            throw new DivideByZeroException("Projective transform produced w=0.");

        return new(MathG.FromDouble<TOut>(x / w), MathG.FromDouble<TOut>(y / w));
    }

    public static Vector2<float> TransformProjectiveF(Matrix3x3<double> m, Vector2<double> v) => TransformProjectiveF<double, double>(m, v);

    public static Vector2<TOut> TransformProjective<TOut>(Matrix3x3<double> m, Vector2<double> v)
        where TOut : unmanaged, INumber<TOut>
        => TransformProjective<TOut, double, double>(m, v);

#endregion
}