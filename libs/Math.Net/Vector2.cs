namespace MarcoZechner.MathDotNet;

/// <summary>
/// A 2D vector using float components (default).
/// </summary>
public readonly partial record struct Vector2()
{
    #region Generic Interaction
    private readonly Vector2<float> _v;
    public float X => _v.X;
    public float Y => _v.Y;

    public Vector2(float x, float y) : this(new Vector2<float>(x, y)) { }
    private Vector2(Vector2<float> v) : this() => _v = v;

    public Vector2<float> AsGeneric() => _v;
    #endregion

    public Vector2 WithX(float x) => new(x, Y);
    public Vector2 WithY(float y) => new(X, y);

    public void Deconstruct(out float x, out float y) { x = X; y = Y; }

    #region Implicit/Explicit Conversions
    public static implicit operator Vector2<float>(Vector2 v) => v._v;
    public static implicit operator Vector2(Vector2<float> v) => new(v);
    public static implicit operator Vector2<double>(Vector2 v) => new(v.X, v.Y);
    public static explicit operator Vector2(Vector2<double> v) => new((float)v.X, (float)v.Y);
    public static implicit operator Vector2<int>(Vector2 v) => new((int)v.X, (int)v.Y);
    public static implicit operator Vector2(Vector2<int> v) => new(v.X, v.Y);
    #endregion

    #region Constants
    public static Vector2 Zero => new(Vector2<float>.Zero);
    public static Vector2 One => new(Vector2<float>.One);
    public static Vector2 Up => new(Vector2<float>.Up);
    public static Vector2 Down => new(Vector2<float>.Down);
    public static Vector2 Left => new(Vector2<float>.Left);
    public static Vector2 Right => new(Vector2<float>.Right);
    #endregion
    public float Length => _v.Length;
    public float SquaredLength => _v.SquaredLength;

    public Vector2 Normalized() => new(_v.Normalized());

    public Vector2 Scalar(float scalar) => new(_v.Scalar(scalar));

    #region Binary Operators
    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a._v + b._v);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a._v - b._v);
    public static Vector2 operator *(Vector2 a, Vector2 b) => new(a._v * b._v);
    public static Vector2 operator /(Vector2 a, Vector2 b) => new(a._v / b._v);
    public static Vector2 operator *(Vector2 a, float scalar) => new(a._v * scalar);
    public static Vector2 operator *(float scalar, Vector2 a) => new(a._v * scalar);
    public static Vector2 operator /(Vector2 a, float scalar) => new(a._v / scalar);
    public static Vector2 operator %(Vector2 a, float scalar) => new(a._v % scalar);
    #endregion

    #region Unary Operators
    public static Vector2 operator -(Vector2 a) => new(-a._v.X, -a._v.Y);
    #endregion

    public override string ToString() => $"Vector2({X}, {Y})";

    #region Static Methods
    public static float Dot(Vector2 a, Vector2 b) => Vector2<float>.Dot(a._v, b._v);

    public static Vector2 Min(Vector2 a, Vector2 b) => Vector2<float>.Min(a._v, b._v);
    public static Vector2 Min(Vector2 a, float b) => Vector2<float>.Min(a._v, b);
    public static Vector2 Max(Vector2 a, Vector2 b) => Vector2<float>.Max(a._v, b._v);
    public static Vector2 Max(Vector2 a, float b) => Vector2<float>.Max(a._v, b);
    public static Vector2 Clamp(Vector2 value, Vector2 min, Vector2 max) => new(Vector2<float>.Clamp(value._v, min._v, max._v));
    public static Vector2 Clamp(Vector2 value, float min, float max) => new(Vector2<float>.Clamp(value._v, min, max));
    public static Vector2 Lerp(Vector2 start, Vector2 end, float t) => new(Vector2<float>.Lerp(start._v, end._v, t));
    #endregion
}

