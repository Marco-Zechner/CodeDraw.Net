namespace MarcoZechner.MathDotNet;

public readonly partial record struct Matrix3x3
{
    private readonly Matrix3x3<float> _m;

    public float M11 => _m.M11;
    public float M12 => _m.M12;
    public float M13 => _m.M13;
    public float M21 => _m.M21;
    public float M22 => _m.M22;
    public float M23 => _m.M23;
    public float M31 => _m.M31;
    public float M32 => _m.M32;
    public float M33 => _m.M33;

    public Matrix3x3(
        float m11, float m12, float m13,
        float m21, float m22, float m23,
        float m31, float m32, float m33)
        => _m = new Matrix3x3<float>(
            m11, m12, m13,
            m21, m22, m23,
            m31, m32, m33);

    public static implicit operator Matrix3x3<float>(Matrix3x3 m) => m._m;
    public static implicit operator Matrix3x3(Matrix3x3<float> m) => new(m);

    private Matrix3x3(Matrix3x3<float> m) => _m = m;

    public static Matrix3x3 Identity => Matrix3x3<float>.Identity;
    public static Matrix3x3 CreateTranslation(float tx, float ty) => Matrix3x3<float>.CreateTranslation(tx, ty);
    public static Matrix3x3 CreateScale(float sx, float sy) => Matrix3x3<float>.CreateScale(sx, sy);
    public static Matrix3x3 CreateRotation(float rotation, AngleUnit angleUnit = AngleUnit.Degrees) => Matrix3x3<float>.CreateRotation(rotation, angleUnit);

    public static Vector2 Transform(Matrix3x3 m, Vector2 v) => Matrix3x3<float>.Transform(m._m, v);
    public static Matrix3x3 operator *(Matrix3x3 a, Matrix3x3 b) => new(a._m * b._m);

    public override string ToString() => _m.ToString();


    #region static methods

    public static Matrix3x3 Lerp(Matrix3x3 start, Matrix3x3 end, float t) => Matrix3x3<float>.Lerp(start._m, end._m, t);

    #endregion
}