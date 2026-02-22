using System.Numerics;

namespace MarcoZechner.MathDotNet;

// ReSharper disable once InconsistentNaming
public readonly partial record struct Matrix3x3
{
#region Returns Number

    public static float Determinant<TA>(Matrix3x3<TA> m)
        where TA : unmanaged, INumber<TA>
        => Matrix3x3<float>.Determinant(m);

    public static TOut Determinant<TOut, TA>(Matrix3x3<TA> m)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        => Matrix3x3<float>.Determinant<TOut, TA>(m);

    public static float Determinant(Matrix3x3<double> m) => Matrix3x3<float>.Determinant(m);

    public static TOut Determinant<TOut>(Matrix3x3<double> m)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.Determinant<TOut>(m);


    public static float Trace<TA>(Matrix3x3<TA> m)
        where TA : unmanaged, INumber<TA>
        => Matrix3x3<float>.Trace(m);

    public static TOut Trace<TOut, TA>(Matrix3x3<TA> m)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        => Matrix3x3<float>.Trace<TOut, TA>(m);

    public static float Trace(Matrix3x3<double> m) => Matrix3x3<float>.Trace(m);

    public static TOut Trace<TOut>(Matrix3x3<double> m)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.Trace<TOut>(m);

#endregion

#region Returns Matrix3X3<TOut>

    public static Matrix3x3<float> CreateRotation<TAng>(TAng angle, AngleUnit unit = AngleUnit.Degrees, RotationDirection direction = RotationDirection.Clockwise)
        where TAng : INumber<TAng>
        => Matrix3x3<float>.CreateRotation(angle, unit, direction);

    public static Matrix3x3<TOut> CreateRotation<TOut, TAng>(TAng angle, AngleUnit unit = AngleUnit.Degrees, RotationDirection direction = RotationDirection.Clockwise)
        where TOut : unmanaged, INumber<TOut>
        where TAng : INumber<TAng>
        => Matrix3x3<float>.CreateRotation<TOut, TAng>(angle, unit, direction);

    public static Matrix3x3<float> CreateRotation(double angle, AngleUnit unit = AngleUnit.Degrees, RotationDirection direction = RotationDirection.Clockwise)
        => Matrix3x3<float>.CreateRotation(angle, unit, direction);

    public static Matrix3x3<TOut> CreateRotation<TOut>(double angle, AngleUnit unit = AngleUnit.Degrees, RotationDirection direction = RotationDirection.Clockwise)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.CreateRotation<TOut>(angle, unit, direction);

    
    public static Matrix3x3<float> CreateTranslation<TTx, TTy>(TTx tx, TTy ty)
        where TTx : INumber<TTx>
        where TTy : INumber<TTy>
        => Matrix3x3<float>.CreateTranslation(tx, ty);

    public static Matrix3x3<TOut> CreateTranslation<TOut, TTx, TTy>(TTx tx, TTy ty)
        where TOut : unmanaged, INumber<TOut>
        where TTx : INumber<TTx>
        where TTy : INumber<TTy>
        => Matrix3x3<float>.CreateTranslation<TOut, TTx, TTy>(tx, ty);

    public static Matrix3x3<float> CreateTranslation(double tx, double ty)
        => Matrix3x3<float>.CreateTranslation(tx, ty);

    public static Matrix3x3<TOut> CreateTranslation<TOut>(double tx, double ty)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.CreateTranslation<TOut>(tx, ty);


    public static Matrix3x3<float> CreateScale<TSx, TSy>(TSx sx, TSy sy)
        where TSx : INumber<TSx>
        where TSy : INumber<TSy>
        => Matrix3x3<float>.CreateScale(sx, sy);

    public static Matrix3x3<TOut> CreateScale<TOut, TSx, TSy>(TSx sx, TSy sy)
        where TOut : unmanaged, INumber<TOut>
        where TSx : INumber<TSx>
        where TSy : INumber<TSy>
        => Matrix3x3<float>.CreateScale<TOut, TSx, TSy>(sx, sy);

    public static Matrix3x3<float> CreateScale(double sx, double sy)
        => Matrix3x3<float>.CreateScale(sx, sy);

    public static Matrix3x3<TOut> CreateScale<TOut>(double sx, double sy)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.CreateScale<TOut>(sx, sy);
    

    public static Matrix3x3<float> CreateBasis<TX, TY, TT>(Vector2<TX> xAxis, Vector2<TY> yAxis, Vector2<TT> translation)
        where TX : unmanaged, INumber<TX>
        where TY : unmanaged, INumber<TY>
        where TT : unmanaged, INumber<TT>
        => Matrix3x3<float>.CreateBasis(xAxis, yAxis, translation);

    public static Matrix3x3<TOut> CreateBasis<TOut, TX, TY, TT>(Vector2<TX> xAxis, Vector2<TY> yAxis, Vector2<TT> translation)
        where TOut : unmanaged, INumber<TOut>
        where TX : unmanaged, INumber<TX>
        where TY : unmanaged, INumber<TY>
        where TT : unmanaged, INumber<TT>
        => Matrix3x3<float>.CreateBasis<TOut, TX, TY, TT>(xAxis, yAxis, translation);

    public static Matrix3x3<float> CreateBasis(Vector2<double> xAxis, Vector2<double> yAxis, Vector2<double> translation)
        => Matrix3x3<float>.CreateBasis(xAxis, yAxis, translation);

    public static Matrix3x3<TOut> CreateBasis<TOut>(Vector2<double> xAxis, Vector2<double> yAxis, Vector2<double> translation)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.CreateBasis<TOut>(xAxis, yAxis, translation);

    public static Matrix3x3<float> CreateShear<TShx, TShy>(TShx shx, TShy shy)
        where TShx : INumber<TShx>
        where TShy : INumber<TShy>
        => Matrix3x3<float>.CreateShear(shx, shy);
    
    public static Matrix3x3<TOut> CreateShear<TOut, TShx, TShy>(TShx shx, TShy shy)
        where TOut : unmanaged, INumber<TOut>
        where TShx : INumber<TShx>
        where TShy : INumber<TShy>
        => Matrix3x3<float>.CreateShear<TOut, TShx, TShy>(shx, shy);
    
    public static Matrix3x3<float> CreateShear(double shx, double shy)
        => Matrix3x3<float>.CreateShear(shx, shy);
    
    public static Matrix3x3<TOut> CreateShear<TOut>(double shx, double shy)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.CreateShear<TOut>(shx, shy);

    public static Matrix3x3<float> Lerp<TA, TB, TT>(Matrix3x3<TA> a, Matrix3x3<TB> b, TT t)
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        where TT : INumber<TT>
        => Matrix3x3<float>.Lerp(a, b, t);

    public static Matrix3x3<TOut> Lerp<TOut, TA, TB, TT>(Matrix3x3<TA> a, Matrix3x3<TB> b, TT t)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        where TB : unmanaged, INumber<TB>
        where TT : INumber<TT>
        => Matrix3x3<float>.Lerp<TOut, TA, TB, TT>(a, b, t);

    public static Matrix3x3<float> Lerp(Matrix3x3<double> a, Matrix3x3<double> b, double t)
        => Matrix3x3<float>.Lerp(a, b, t);

    public static Matrix3x3<TOut> Lerp<TOut>(Matrix3x3<double> a, Matrix3x3<double> b, double t)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.Lerp<TOut>(a, b, t);


    public static Matrix3x3<float> Transpose<TA>(Matrix3x3<TA> m)
        where TA : unmanaged, INumber<TA>
        => Matrix3x3<float>.Transpose(m);

    public static Matrix3x3<TOut> Transpose<TOut, TA>(Matrix3x3<TA> m)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        => Matrix3x3<float>.Transpose<TOut, TA>(m);

    public static Matrix3x3<float> Transpose(Matrix3x3<double> m) => Matrix3x3<float>.Transpose(m);

    public static Matrix3x3<TOut> Transpose<TOut>(Matrix3x3<double> m)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.Transpose<TOut>(m);


    public static bool TryInvert<TA>(Matrix3x3<TA> m, out Matrix3x3<float> inv)
        where TA : unmanaged, INumber<TA>
    {
        var ok = Matrix3x3<float>.TryInvert(m, out var i);
        inv = i;
        return ok;
    }

    public static bool TryInvert<TOut, TA>(Matrix3x3<TA> m, out Matrix3x3<TOut> inv)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        => Matrix3x3<float>.TryInvert(m, out inv);

    public static bool TryInvert(Matrix3x3<double> m, out Matrix3x3<float> inv)
    {
        var ok = Matrix3x3<float>.TryInvert(m, out var i);
        inv = i;
        return ok;
    }

    public static bool TryInvert<TOut>(Matrix3x3<double> m, out Matrix3x3<TOut> inv)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.TryInvert<TOut, double>(m, out inv);
    
    public static Matrix3x3<float> Invert<TA>(Matrix3x3<TA> m)
        where TA : unmanaged, INumber<TA>
        => Matrix3x3<float>.Invert(m);

    public static Matrix3x3<TOut> Invert<TOut, TA>(Matrix3x3<TA> m)
        where TOut : unmanaged, INumber<TOut>
        where TA : unmanaged, INumber<TA>
        => Matrix3x3<float>.Invert<TOut, TA>(m);

    public static Matrix3x3<float> Invert(Matrix3x3<double> m)
        => Matrix3x3<float>.Invert(m);

    public static Matrix3x3<TOut> Invert<TOut>(Matrix3x3<double> m)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.Invert<TOut, double>(m);

    
    public static Vector2<float> TransformAffine<TM, TV>(Matrix3x3<TM> m, Vector2<TV> v)
        where TM : unmanaged, INumber<TM>
        where TV : unmanaged, INumber<TV>
        => Matrix3x3<float>.TransformAffine(m, v);
    
    public static Vector2<TOut> TransformAffine<TOut, TM, TV>(Matrix3x3<TM> m, Vector2<TV> v)
        where TOut : unmanaged, INumber<TOut>
        where TM : unmanaged, INumber<TM>
        where TV : unmanaged, INumber<TV>
        => Matrix3x3<float>.TransformAffine<TOut, TM, TV>(m, v);
    
    public static Vector2<float> TransformAffine(Matrix3x3<double> m, Vector2<double> v)
        => Matrix3x3<float>.TransformAffine(m, v);

    public static Vector2<TOut> TransformAffine<TOut>(Matrix3x3<double> m, Vector2<double> v)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.TransformAffine<TOut, double, double>(m, v);
    

    public static Vector2<float> TransformProjective<TM, TV>(Matrix3x3<TM> m, Vector2<TV> v)
        where TM : unmanaged, INumber<TM>
        where TV : unmanaged, INumber<TV>
        => Matrix3x3<float>.TransformProjective(m, v);

    public static Vector2<TOut> TransformProjective<TOut, TM, TV>(Matrix3x3<TM> m, Vector2<TV> v)
        where TOut : unmanaged, INumber<TOut>
        where TM : unmanaged, INumber<TM>
        where TV : unmanaged, INumber<TV>
        => Matrix3x3<float>.TransformProjective<TOut, TM, TV>(m, v);
    
    public static Vector2<float> TransformProjective(Matrix3x3<double> m, Vector2<double> v)
        => Matrix3x3<float>.TransformProjective(m, v);

    public static Vector2<TOut> TransformProjective<TOut>(Matrix3x3<double> m, Vector2<double> v)
        where TOut : unmanaged, INumber<TOut>
        => Matrix3x3<double>.TransformProjective<TOut, double, double>(m, v);

#endregion
}