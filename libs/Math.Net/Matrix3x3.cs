namespace MarcoZechner.MathDotNet;

/// <summary>
/// A 3x3 matrix using float components (default).
/// Hosts the "main" static API surface as well.
/// </summary>
// ReSharper disable once InconsistentNaming
public readonly partial record struct Matrix3x3(
    float M11, float M12, float M13,
    float M21, float M22, float M23,
    float M31, float M32, float M33)
{
#region Generic Interaction

    public Matrix3x3<float> AsGeneric() => new(
        M11, M12, M13,
        M21, M22, M23,
        M31, M32, M33
    );

#endregion

#region Conversions

    public static implicit operator Matrix3x3<float>(Matrix3x3 m) => m.AsGeneric();

    public static implicit operator Matrix3x3(Matrix3x3<float> m) => new(
        m.M11, m.M12, m.M13,
        m.M21, m.M22, m.M23,
        m.M31, m.M32, m.M33
    );

    public static implicit operator Matrix3x3<double>(Matrix3x3 m) => new(
        m.M11, m.M12, m.M13,
        m.M21, m.M22, m.M23,
        m.M31, m.M32, m.M33
    );

#endregion

#region Constants / Factories

    public static Matrix3x3 Identity => new(
        1f, 0f, 0f,
        0f, 1f, 0f,
        0f, 0f, 1f
    );

#endregion

#region Operators

    public static Matrix3x3 operator *(Matrix3x3 a, Matrix3x3 b)
    {
        // If your generic version already has the correct/optimized implementation,
        // reuse it to avoid duplicating multiplication logic here.
        var r = a.AsGeneric() * b.AsGeneric();
        return new Matrix3x3(
            r.M11, r.M12, r.M13,
            r.M21, r.M22, r.M23,
            r.M31, r.M32, r.M33
        );
    }

#endregion
    
    public override string ToString() => AsGeneric().ToString();
    
#region Static Methods (returns Matrix3x3 and Vector2)
    
    public static Matrix3x3 CreateRotation(float angle, AngleUnit unit = AngleUnit.Degrees) => CreateRotation<float>(angle, unit);
    public static Matrix3x3 CreateTranslation(float tx, float ty) => CreateTranslation<float>(tx, ty);
    public static Matrix3x3 CreateScale(float sx, float sy) => CreateScale<float>(sx, sy);
    public static Matrix3x3 CreateBasis(Vector2 xAxis, Vector2 yAxis, Vector2 translation) => CreateBasis<float>(xAxis, yAxis, translation);
    public static Matrix3x3 CreateShear(float shx, float shy) => CreateShear<float>(shx, shy);
    public static Matrix3x3 Lerp(Matrix3x3 a, Matrix3x3 b, float t) => Lerp<float>(a, b, t);
    public static Matrix3x3 Transpose(Matrix3x3 m) => Transpose<float, float>(m);
    public static bool TryInvert(Matrix3x3 m, out Matrix3x3 inverse)
    {
        var result = TryInvert<float, float>(m, out var inverseOut);
        inverse = inverseOut;
        return result;
    }
    public static Matrix3x3 Invert(Matrix3x3 m) => Invert<float, float>(m);
    public static Vector2 TransformAffine(Matrix3x3 m, Vector2 v) => TransformAffine<float>(m, v);
    public static Vector2 TransformProjective(Matrix3x3 m, Vector2 v) => TransformProjective<float>(m, v);
    
#endregion

}