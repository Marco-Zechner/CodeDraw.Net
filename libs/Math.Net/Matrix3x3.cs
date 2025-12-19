namespace MarcoZechner.MathDotNet;

public readonly partial record struct Matrix3X3
{
    private readonly Matrix3X3<float> _m;

    public float M11 => _m.M11;
    public float M12 => _m.M12;
    public float M13 => _m.M13;
    public float M21 => _m.M21;
    public float M22 => _m.M22;
    public float M23 => _m.M23;
    public float M31 => _m.M31;
    public float M32 => _m.M32;
    public float M33 => _m.M33;

    public Matrix3X3(
        float m11, float m12, float m13,
        float m21, float m22, float m23,
        float m31, float m32, float m33)
        => _m = new Matrix3X3<float>(
            m11, m12, m13,
            m21, m22, m23,
            m31, m32, m33);

    public static implicit operator Matrix3X3<float>(Matrix3X3 m) => m._m;
    public static implicit operator Matrix3X3(Matrix3X3<float> m) => new(m);

    private Matrix3X3(Matrix3X3<float> m) => _m = m;

    public static Matrix3X3 Identity => Matrix3X3<float>.Identity;
    public static Matrix3X3 CreateTranslation(float tx, float ty) => Matrix3X3<float>.CreateTranslation(tx, ty);
    public static Matrix3X3 CreateScale(float sx, float sy) => Matrix3X3<float>.CreateScale(sx, sy);
    public static Matrix3X3 CreateRotation(float rotation, AngleUnit angleUnit = AngleUnit.DEGREES) => Matrix3X3<float>.CreateRotation(rotation, angleUnit);

    public static Vector2 Transform(Matrix3X3 m, Vector2 v) => Matrix3X3<float>.Transform(m._m, v);
    public static Matrix3X3 operator *(Matrix3X3 a, Matrix3X3 b) => new(a._m * b._m);

    public override string ToString() => _m.ToString();


    #region static methods

    public static Matrix3X3 Lerp(Matrix3X3 start, Matrix3X3 end, float t) => Matrix3X3<float>.Lerp(start._m, end._m, t);

    #endregion
}