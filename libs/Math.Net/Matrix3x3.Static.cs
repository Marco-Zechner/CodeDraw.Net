using System.Numerics;

namespace MarcoZechner.MathDotNet;

public readonly partial record struct Matrix3x3
{
#region Returns Number

    public static float DeterminantF<TA>(Matrix3x3<TA> m)
        where TA : unmanaged, INumber<TA>
        => Matrix3x3<float>.DeterminantF(m);

    public static TOut Determinant<TOut, TA>(Matrix3x3<TA> m)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        => Matrix3x3<float>.Determinant<TOut, TA>(m);

    public static float DeterminantF(Matrix3x3<double> m) => Matrix3x3<float>.DeterminantF(m);

    public static TOut Determinant<TOut>(Matrix3x3<double> m)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.Determinant<TOut>(m);


    public static float TraceF<TA>(Matrix3x3<TA> m)
        where TA : unmanaged, INumber<TA>
        => Matrix3x3<float>.TraceF(m);

    public static TOut Trace<TOut, TA>(Matrix3x3<TA> m)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        => Matrix3x3<float>.Trace<TOut, TA>(m);

    public static float TraceF(Matrix3x3<double> m) => Matrix3x3<float>.TraceF(m);

    public static TOut Trace<TOut>(Matrix3x3<double> m)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.Trace<TOut>(m);

#endregion

#region Returns Matrix3X3<TOut>

    public static Matrix3x3 CreateRotationF<TAng>(TAng angle, AngleUnit unit = AngleUnit.Degrees)
        where TAng : INumber<TAng>
        => Matrix3x3<float>.CreateRotationF(angle, unit);

    public static Matrix3x3<TOut> CreateRotation<TOut, TAng>(TAng angle, AngleUnit unit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        where TAng : INumber<TAng>
        => Matrix3x3<float>.CreateRotation<TOut, TAng>(angle, unit);

    public static Matrix3x3 CreateRotationF(double angle, AngleUnit unit = AngleUnit.Degrees)
        => Matrix3x3<float>.CreateRotationF(angle, unit);

    public static Matrix3x3<TOut> CreateRotation<TOut>(double angle, AngleUnit unit = AngleUnit.Degrees)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.CreateRotation<TOut>(angle, unit);

    
    public static Matrix3x3 CreateTranslationF<TTx, TTy>(TTx tx, TTy ty)
        where TTx : INumber<TTx>
        where TTy : INumber<TTy>
        => Matrix3x3<float>.CreateTranslationF(tx, ty);

    public static Matrix3x3<TOut> CreateTranslation<TOut, TTx, TTy>(TTx tx, TTy ty)
        where TOut : unmanaged, INumber<TOut>
        where TTx : INumber<TTx>
        where TTy : INumber<TTy>
        => Matrix3x3<float>.CreateTranslation<TOut, TTx, TTy>(tx, ty);

    public static Matrix3x3 CreateTranslationF(double tx, double ty)
        => Matrix3x3<float>.CreateTranslationF(tx, ty);

    public static Matrix3x3<TOut> CreateTranslation<TOut>(double tx, double ty)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.CreateTranslation<TOut>(tx, ty);


    public static Matrix3x3 CreateScaleF<TSx, TSy>(TSx sx, TSy sy)
        where TSx : INumber<TSx>
        where TSy : INumber<TSy>
        => Matrix3x3<float>.CreateScaleF(sx, sy);

    public static Matrix3x3<TOut> CreateScale<TOut, TSx, TSy>(TSx sx, TSy sy)
        where TOut : unmanaged, INumber<TOut>
        where TSx : INumber<TSx>
        where TSy : INumber<TSy>
        => Matrix3x3<float>.CreateScale<TOut, TSx, TSy>(sx, sy);

    public static Matrix3x3 CreateScaleF(double sx, double sy)
        => Matrix3x3<float>.CreateScaleF(sx, sy);

    public static Matrix3x3<TOut> CreateScale<TOut>(double sx, double sy)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.CreateScale<TOut>(sx, sy);
    

    public static Matrix3x3 CreateBasisF<TX, TY, TT>(Vector2<TX> xAxis, Vector2<TY> yAxis, Vector2<TT> translation)
        where TX : unmanaged, INumber<TX>
        where TY : unmanaged, INumber<TY>
        where TT : unmanaged, INumber<TT>
        => Matrix3x3<float>.CreateBasisF(xAxis, yAxis, translation);

    public static Matrix3x3<TOut> CreateBasis<TOut, TX, TY, TT>(Vector2<TX> xAxis, Vector2<TY> yAxis, Vector2<TT> translation)
        where TOut : unmanaged, INumber<TOut>
        where TX : unmanaged, INumber<TX>
        where TY : unmanaged, INumber<TY>
        where TT : unmanaged, INumber<TT>
        => Matrix3x3<float>.CreateBasis<TOut, TX, TY, TT>(xAxis, yAxis, translation);

    public static Matrix3x3 CreateBasisF(Vector2<double> xAxis, Vector2<double> yAxis, Vector2<double> translation)
        => Matrix3x3<float>.CreateBasisF(xAxis, yAxis, translation);

    public static Matrix3x3<TOut> CreateBasis<TOut>(Vector2<double> xAxis, Vector2<double> yAxis, Vector2<double> translation)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.CreateBasis<TOut>(xAxis, yAxis, translation);


    public static Matrix3x3 LerpF<TA, TB, TT>(Matrix3x3<TA> a, Matrix3x3<TB> b, TT t)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        where TT : INumber<TT>
        => Matrix3x3<float>.LerpF(a, b, t);

    public static Matrix3x3<TOut> Lerp<TOut, TA, TB, TT>(Matrix3x3<TA> a, Matrix3x3<TB> b, TT t)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        where TT : INumber<TT>
        => Matrix3x3<float>.Lerp<TOut, TA, TB, TT>(a, b, t);

    public static Matrix3x3 LerpF(Matrix3x3<double> a, Matrix3x3<double> b, double t)
        => Matrix3x3<float>.LerpF(a, b, t);

    public static Matrix3x3<TOut> Lerp<TOut>(Matrix3x3<double> a, Matrix3x3<double> b, double t)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.Lerp<TOut>(a, b, t);


    public static Matrix3x3 TransposeF<TA>(Matrix3x3<TA> m)
        where TA : unmanaged, INumber<TA>
        => Matrix3x3<float>.TransposeF(m);

    public static Matrix3x3<TOut> Transpose<TOut, TA>(Matrix3x3<TA> m)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        => Matrix3x3<float>.Transpose<TOut, TA>(m);

    public static Matrix3x3 TransposeF(Matrix3x3<double> m) => Matrix3x3<float>.TransposeF(m);

    public static Matrix3x3<TOut> Transpose<TOut>(Matrix3x3<double> m)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.Transpose<TOut>(m);


    public static bool TryInvertF<TA>(Matrix3x3<TA> m, out Matrix3x3 inv)
        where TA : unmanaged, INumber<TA>
    {
        var ok = Matrix3x3<float>.TryInvertF(m, out var i);
        inv = i;
        return ok;
    }

    public static bool TryInvert<TOut, TA>(Matrix3x3<TA> m, out Matrix3x3<TOut> inv)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        => Matrix3x3<float>.TryInvert(m, out inv);

    public static bool TryInvertF(Matrix3x3<double> m, out Matrix3x3 inv)
    {
        var ok = Matrix3x3<float>.TryInvertF(m, out var i);
        inv = i;
        return ok;
    }

    public static bool TryInvert<TOut>(Matrix3x3<double> m, out Matrix3x3<TOut> inv)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.TryInvert<TOut, double>(m, out inv);
    
    public static Matrix3x3 InvertF<TA>(Matrix3x3<TA> m)
        where TA : unmanaged, INumber<TA>
        => Matrix3x3<float>.InvertF(m);

    public static Matrix3x3<TOut> Invert<TOut, TA>(Matrix3x3<TA> m)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        => Matrix3x3<float>.Invert<TOut, TA>(m);

    public static Matrix3x3 InvertF(Matrix3x3<double> m)
        => Matrix3x3<float>.InvertF(m);

    public static Matrix3x3<TOut> Invert<TOut>(Matrix3x3<double> m)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.Invert<TOut, double>(m);

    
    public static Vector2<float> TransformAffineF<TM, TV>(Matrix3x3<TM> m, Vector2<TV> v)
        where TM : unmanaged, INumber<TM>
        where TV : unmanaged, INumber<TV>
        => Matrix3x3<float>.TransformAffineF(m, v);
    
    public static Vector2<TOut> TransformAffine<TOut, TM, TV>(Matrix3x3<TM> m, Vector2<TV> v)
        where TOut : unmanaged, INumber<TOut>
        where TM : unmanaged, INumber<TM>
        where TV : unmanaged, INumber<TV>
        => Matrix3x3<float>.TransformAffine<TOut, TM, TV>(m, v);
    
    public static Vector2<float> TransformAffineF(Matrix3x3<double> m, Vector2<double> v)
        => Matrix3x3<float>.TransformAffineF(m, v);

    public static Vector2<TOut> TransformAffine<TOut>(Matrix3x3<double> m, Vector2<double> v)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.TransformAffine<TOut, double, double>(m, v);
    

    public static Vector2<float> TransformProjectiveF<TM, TV>(Matrix3x3<TM> m, Vector2<TV> v)
        where TM : unmanaged, INumber<TM>
        where TV : unmanaged, INumber<TV>
        => Matrix3x3<float>.TransformProjectiveF(m, v);

    public static Vector2<TOut> TransformProjective<TOut, TM, TV>(Matrix3x3<TM> m, Vector2<TV> v)
        where TOut : unmanaged, INumber<TOut>
        where TM : unmanaged, INumber<TM>
        where TV : unmanaged, INumber<TV>
        => Matrix3x3<float>.TransformProjective<TOut, TM, TV>(m, v);
    
    public static Vector2<float> TransformProjectiveF(Matrix3x3<double> m, Vector2<double> v)
        => Matrix3x3<float>.TransformProjectiveF(m, v);

    public static Vector2<TOut> TransformProjective<TOut>(Matrix3x3<double> m, Vector2<double> v)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.TransformProjective<TOut, double, double>(m, v);

#endregion
}