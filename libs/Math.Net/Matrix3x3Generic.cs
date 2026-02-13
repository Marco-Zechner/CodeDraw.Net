using System.Numerics;

namespace MarcoZechner.MathDotNet;

public readonly partial record struct Matrix3X3<T>
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

    public Matrix3X3(
        T m11, T m12, T m13,
        T m21, T m22, T m23,
        T m31, T m32, T m33)
        => (M11, M12, M13, M21, M22, M23, M31, M32, M33)
         = (m11, m12, m13, m21, m22, m23, m31, m32, m33);

    public static Matrix3X3<T> Identity => new(
        T.One, T.Zero, T.Zero,
        T.Zero, T.One, T.Zero,
        T.Zero, T.Zero, T.One);

    public static Matrix3X3<T> CreateTranslation(T tx, T ty) => new(
        T.One, T.Zero, tx,
        T.Zero, T.One, ty,
        T.Zero, T.Zero, T.One);

    public static Matrix3X3<T> CreateScale(T sx, T sy) => new(
        sx, T.Zero, T.Zero,
        T.Zero, sy, T.Zero,
        T.Zero, T.Zero, T.One);

    public static Matrix3X3<T> CreateRotation(T rotation, AngleUnit angleUnit = AngleUnit.DEGREES)
    {
        double radians = angleUnit == AngleUnit.DEGREES ?
            double.CreateChecked(rotation) * (System.Math.PI / 180) :
            double.CreateChecked(rotation);

        var cos = T.CreateChecked(System.Math.Cos(radians));
        var sin = T.CreateChecked(System.Math.Sin(radians));
        return new(
            cos, -sin, T.Zero,
            sin, cos, T.Zero,
            T.Zero, T.Zero, T.One);
    }

    public static Vector2<T> TransformAffine(Matrix3X3<T> matrix, Vector2<T> vector)
    {
        var x = matrix.M11 * vector.X + matrix.M12 * vector.Y + matrix.M13;
        var y = matrix.M21 * vector.X + matrix.M22 * vector.Y + matrix.M23;
        return new(x, y);
    }

    public static Matrix3X3<T> operator *(Matrix3X3<T> a, Matrix3X3<T> b)
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

    public static Matrix3X3<T> Lerp(Matrix3X3<T> start, Matrix3X3<T> end, T t) {
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
    
    public static T Determinant(Matrix3X3<T> m)
    {
        // Rule of Sarrus / cofactor expansion
        return
            m.M11 * (m.M22 * m.M33 - m.M23 * m.M32) -
            m.M12 * (m.M21 * m.M33 - m.M23 * m.M31) +
            m.M13 * (m.M21 * m.M32 - m.M22 * m.M31);
    }

    public static bool TryInvert(Matrix3X3<T> m, out Matrix3X3<T> inv)
    {
        var det = Determinant(m);
        // For generic INumber<T>, compare to zero carefully.
        if (det == T.Zero)
        {
            inv = Identity;
            return false;
        }

        var invDet = T.One / det;

        // Adjugate (transpose of cofactor matrix) * (1/det)
        var i11 =  (m.M22 * m.M33 - m.M23 * m.M32) * invDet;
        var i12 = -(m.M12 * m.M33 - m.M13 * m.M32) * invDet;
        var i13 =  (m.M12 * m.M23 - m.M13 * m.M22) * invDet;

        var i21 = -(m.M21 * m.M33 - m.M23 * m.M31) * invDet;
        var i22 =  (m.M11 * m.M33 - m.M13 * m.M31) * invDet;
        var i23 = -(m.M11 * m.M23 - m.M13 * m.M21) * invDet;

        var i31 =  (m.M21 * m.M32 - m.M22 * m.M31) * invDet;
        var i32 = -(m.M11 * m.M32 - m.M12 * m.M31) * invDet;
        var i33 =  (m.M11 * m.M22 - m.M12 * m.M21) * invDet;

        inv = new Matrix3X3<T>(
            i11, i12, i13,
            i21, i22, i23,
            i31, i32, i33
        );
        return true;
    }

    public static Matrix3X3<T> Invert(Matrix3X3<T> m)
    {
        if (!TryInvert(m, out var inv))
            throw new InvalidOperationException("Matrix is not invertible.");
        return inv;
    }

    /// <summary>
    /// Full projective transform (homogeneous divide).
    /// If w becomes 0, this throws.
    /// </summary>
    public static Vector2<T> TransformProjective(Matrix3X3<T> m, Vector2<T> v)
    {
        var x = m.M11 * v.X + m.M12 * v.Y + m.M13;
        var y = m.M21 * v.X + m.M22 * v.Y + m.M23;
        var w = m.M31 * v.X + m.M32 * v.Y + m.M33;

        if (w == T.Zero)
            throw new DivideByZeroException("Projective transform produced w=0.");

        return new Vector2<T>(x / w, y / w);
    }

    
    #endregion
} 