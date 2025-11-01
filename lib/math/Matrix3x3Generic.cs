using System.Numerics;

namespace MarcoZechner.MathDotNet;

public readonly partial record struct Matrix3x3<T>
    where T : unmanaged, INumber<T>
{
    public T M11 { get; init; }
    public T M12 { get; init; }
    public T M13 { get; init; }
    public T M21 { get; init; }
    public T M22 { get; init; }
    public T M23 { get; init; }
    public T M31 { get; init; }
    public T M32 { get; init; }
    public T M33 { get; init; }

    public Matrix3x3(
        T m11, T m12, T m13,
        T m21, T m22, T m23,
        T m31, T m32, T m33)
        => (M11, M12, M13, M21, M22, M23, M31, M32, M33)
         = (m11, m12, m13, m21, m22, m23, m31, m32, m33);

    public static Matrix3x3<T> Identity => new(
        T.One, T.Zero, T.Zero,
        T.Zero, T.One, T.Zero,
        T.Zero, T.Zero, T.One);

    public static Matrix3x3<T> CreateTranslation(T tx, T ty) => new(
        T.One, T.Zero, tx,
        T.Zero, T.One, ty,
        T.Zero, T.Zero, T.One);

    public static Matrix3x3<T> CreateScale(T sx, T sy) => new(
        sx, T.Zero, T.Zero,
        T.Zero, sy, T.Zero,
        T.Zero, T.Zero, T.One);

    public static Matrix3x3<T> CreateRotation(T rotation, AngleUnit angleUnit = AngleUnit.Degrees)
    {
        double radians = angleUnit == AngleUnit.Degrees ?
            double.CreateChecked(rotation) * (System.Math.PI / 180) :
            double.CreateChecked(rotation);

        var cos = T.CreateChecked(System.Math.Cos(radians));
        var sin = T.CreateChecked(System.Math.Sin(radians));
        return new(
            cos, -sin, T.Zero,
            sin, cos, T.Zero,
            T.Zero, T.Zero, T.One);
    }

    public static Vector2<T> Transform(Matrix3x3<T> matrix, Vector2<T> vector)
    {
        var x = matrix.M11 * vector.X + matrix.M12 * vector.Y + matrix.M13;
        var y = matrix.M21 * vector.X + matrix.M22 * vector.Y + matrix.M23;
        return new(x, y);
    }

    public static Matrix3x3<T> operator *(Matrix3x3<T> a, Matrix3x3<T> b)
    {
        return new(
            a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31,
            a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32,
            a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33,

            a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31,
            a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32,
            a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33,

            a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31,
            a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32,
            a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33
        );
    }

    public override string ToString() =>
        $"[{M11}, {M12}, {M13}]\n [{M21}, {M22}, {M23}]\n [{M31}, {M32}, {M33}]";


    #region static methods

    public static Matrix3x3<T> Lerp(Matrix3x3<T> start, Matrix3x3<T> end, T t) {
        return new(
            MathG.Lerp(start.M11, end.M11, t),
            MathG.Lerp(start.M12, end.M12, t),
            MathG.Lerp(start.M13, end.M13, t),

            MathG.Lerp(start.M21, end.M21, t),
            MathG.Lerp(start.M22, end.M22, t),
            MathG.Lerp(start.M23, end.M23, t),

            MathG.Lerp(start.M31, end.M31, t),
            MathG.Lerp(start.M32, end.M32, t),
            MathG.Lerp(start.M33, end.M33, t));
    }
    #endregion
} 