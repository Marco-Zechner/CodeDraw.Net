using System.Numerics;

namespace MarcoZechner.MathDotNet;

public readonly partial record struct Matrix3x3<T>(
    T M11, T M12, T M13,
    T M21, T M22, T M23,
    T M31, T M32, T M33
) where T : unmanaged, INumber<T>
{
#region Constants

    public static Matrix3x3<T> Identity => new(
        T.One,  T.Zero, T.Zero,
        T.Zero, T.One,  T.Zero,
        T.Zero, T.Zero, T.One);

#endregion

#region Transform (T domain)

    /// <summary>Affine transform (assumes last row is [0,0,1]).</summary>
    public static Vector2<T> TransformAffine(Matrix3x3<T> m, Vector2<T> v)
    {
        var x = m.M11 * v.X + m.M12 * v.Y + m.M13;
        var y = m.M21 * v.X + m.M22 * v.Y + m.M23;
        return new Vector2<T>(x, y);
    }

    /// <summary>
    /// Full projective transform (homogeneous divide). Throws if w becomes 0.
    /// </summary>
    public static Vector2<T> TransformProjective(Matrix3x3<T> m, Vector2<T> v)
    {
        var x = m.M11 * v.X + m.M12 * v.Y + m.M13;
        var y = m.M21 * v.X + m.M22 * v.Y + m.M23;
        var w = m.M31 * v.X + m.M32 * v.Y + m.M33;

        if (w == T.Zero)
            throw new DivideByZeroException("Projective transform produced w=0.");

        return new Vector2<T>(x / w, y / w);
    }

#endregion

#region Operators (T domain)

    public static Matrix3x3<T> operator *(Matrix3x3<T> a, Matrix3x3<T> b)
        => new(
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

#endregion

    public override string ToString()
        => $"[{M11}, {M12}, {M13}]\n [{M21}, {M22}, {M23}]\n [{M31}, {M32}, {M33}]";
}