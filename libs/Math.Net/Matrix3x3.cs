namespace MarcoZechner.MathDotNet;

/// <summary>
/// A 3x3 matrix using float components (default).
/// Hosts the "main" static API surface as well.
/// </summary>
public readonly partial record struct Matrix3x3
{
#region Generic Interaction

    private readonly Matrix3x3<float> _m;

    public float M11 => _m.M11; public float M12 => _m.M12; public float M13 => _m.M13;
    public float M21 => _m.M21; public float M22 => _m.M22; public float M23 => _m.M23;
    public float M31 => _m.M31; public float M32 => _m.M32; public float M33 => _m.M33;

    public Matrix3x3(
        float m11, float m12, float m13,
        float m21, float m22, float m23,
        float m31, float m32, float m33)
        => _m = new(
            m11, m12, m13,
            m21, m22, m23,
            m31, m32, m33
        );

    private Matrix3x3(Matrix3x3<float> m) => _m = m;

    public Matrix3x3<float> AsGeneric() => _m;

#endregion

#region Conversions

    public static implicit operator Matrix3x3<float>(Matrix3x3 m) => m._m;
    public static implicit operator Matrix3x3(Matrix3x3<float> m) => new(m);

    public static implicit operator Matrix3x3<double>(Matrix3x3 m) => new(
        m.M11, m.M12, m.M13,
        m.M21, m.M22, m.M23,
        m.M31, m.M32, m.M33
    );

#endregion

#region Constants / Factories

    public static Matrix3x3 Identity => Matrix3x3<float>.Identity;

#endregion

#region Operators

    public static Matrix3x3 operator *(Matrix3x3 a, Matrix3x3 b) => new(a._m * b._m);

#endregion

    public override string ToString() => _m.ToString();
}