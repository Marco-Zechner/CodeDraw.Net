using System.Numerics;

namespace MarcoZechner.MathDotNet;

public readonly partial record struct Vector2<T>(T X, T Y) where T : unmanaged, INumber<T>
{

    public Vector2(Vector2<T> v) : this(v.X, v.Y) { }

    public Vector2<T> WithX(T x) => new(x, Y);
    public Vector2<T> WithY(T y) => new(X, y);

    public void Deconstruct(out T x, out T y) { x = X; y = Y; }

    #region Implicit/Explicit Conversions
    public static explicit operator Vector2<float>(Vector2<T> v) =>
        new(float.CreateChecked(v.X), float.CreateChecked(v.Y));

    public static explicit operator Vector2<double>(Vector2<T> v) =>
        new(double.CreateChecked(v.X), double.CreateChecked(v.Y));

    public static explicit operator Vector2<int>(Vector2<T> v) =>
        new(int.CreateChecked(v.X), int.CreateChecked(v.Y));
    #endregion

    #region Constants
    public static Vector2<T> Zero => new(T.Zero, T.Zero);
    public static Vector2<T> One => new(T.One, T.One);
    public static Vector2<T> Up => new(T.Zero, T.One);
    public static Vector2<T> Down => new(T.Zero, -T.One);
    public static Vector2<T> Left => new(-T.One, T.Zero);
    public static Vector2<T> Right => new(T.One, T.Zero);
    #endregion

    public T Length => T.CreateChecked(System.Math.Sqrt(double.CreateChecked(X * X + Y * Y)));
    public T SquaredLength => X * X + Y * Y;

    public Vector2<T> Normalized()
    {
        var length = Length;
        return new(X / length, Y / length);
    }

    public Vector2<T> Scalar(T scalar) => this * scalar;

    #region Binary Operators
    public static Vector2<T> operator +(Vector2<T> a, Vector2<T> b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2<T> operator -(Vector2<T> a, Vector2<T> b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2<T> operator *(Vector2<T> a, Vector2<T> b) => new(a.X * b.X, a.Y * b.Y);
    public static Vector2<T> operator /(Vector2<T> a, Vector2<T> b) {
        if (b.X.Equals(T.Zero) || b.Y.Equals(T.Zero))
            throw new DivideByZeroException("Cannot divide by zero in Vector2 division.");
        return new(a.X / b.X, a.Y / b.Y);
    }
    public static Vector2<T> operator *(Vector2<T> a, T scalar) => new(a.X * scalar, a.Y * scalar);
    public static Vector2<float> operator *(Vector2<T> a, float scalar) => new(float.CreateChecked(a.X) * scalar, float.CreateChecked(a.Y) * scalar);
    public static Vector2<double> operator *(Vector2<T> a, double scalar) => new(double.CreateChecked(a.X) * scalar, double.CreateChecked(a.Y) * scalar);
    public static Vector2<T> operator *(T scalar, Vector2<T> a) => new(a.X * scalar, a.Y * scalar);
    public static Vector2<float> operator *(float scalar, Vector2<T> a) => new(float.CreateChecked(a.X) * scalar, float.CreateChecked(a.Y) * scalar);
    public static Vector2<double> operator *(double scalar, Vector2<T> a) => new(double.CreateChecked(a.X) * scalar, double.CreateChecked(a.Y) * scalar);
    public static Vector2<T> operator /(Vector2<T> a, T scalar)
    {
        if (scalar.Equals(T.Zero))
            throw new DivideByZeroException("Cannot divide by zero in Vector2 division.");
        return new(a.X / scalar, a.Y / scalar);
    }
    public static Vector2<float> operator /(Vector2<T> a, float scalar)
    {
        if (scalar.Equals(0f))
            throw new DivideByZeroException("Cannot divide by zero in Vector2 division.");
        return new(float.CreateChecked(a.X) / scalar, float.CreateChecked(a.Y) / scalar);
    }
    public static Vector2<double> operator /(Vector2<T> a, double scalar)
    {
        if (scalar.Equals(0f))
            throw new DivideByZeroException("Cannot divide by zero in Vector2 division.");
        return new(double.CreateChecked(a.X) / scalar, double.CreateChecked(a.Y) / scalar);
    }
    public static Vector2<T> operator %(Vector2<T> a, T scalar)
    {
        if (scalar.Equals(T.Zero))
            throw new DivideByZeroException("Cannot divide by zero in Vector2 division.");
        return new(a.X % scalar, a.Y % scalar);
    }
    public static Vector2<float> operator %(Vector2<T> a, float scalar)
    {
        if (scalar.Equals(0f))
            throw new DivideByZeroException("Cannot divide by zero in Vector2 division.");
        return new(float.CreateChecked(a.X) % scalar, float.CreateChecked(a.Y) % scalar);
    }
    public static Vector2<double> operator %(Vector2<T> a, double scalar)
    {
        if (scalar.Equals(0.0))
            throw new DivideByZeroException("Cannot divide by zero in Vector2 division.");
        return new(double.CreateChecked(a.X) % scalar, double.CreateChecked(a.Y) % scalar);
    }
    #endregion

    #region Unary Operators
    public static Vector2<T> operator -(Vector2<T> a) => new(-a.X, -a.Y);
    #endregion

    public override string ToString() => $"Vector2<{typeof(T).Name}>({X}, {Y})";


    #region Static Methods
    public static T Dot(Vector2<T> a, Vector2<T> b) => a.X * b.X + a.Y * b.Y;

    public static Vector2<T> Min(Vector2<T> a, Vector2<T> b) => new(T.Min(a.X, b.X), T.Min(a.Y, b.Y));
    public static Vector2<T> Min(Vector2<T> a, T b) => new(T.Min(a.X, b), T.Min(a.Y, b));
    public static Vector2<T> Max(Vector2<T> a, Vector2<T> b) => new(T.Max(a.X, b.X), T.Max(a.Y, b.Y));
    public static Vector2<T> Max(Vector2<T> a, T b) => new(T.Max(a.X, b), T.Max(a.Y, b));
    public static Vector2<T> Clamp(Vector2<T> value, Vector2<T> min, Vector2<T> max) => new(
        T.Clamp(value.X, min.X, max.X),
        T.Clamp(value.Y, min.Y, max.Y)
    );
    public static Vector2<T> Clamp(Vector2<T> value, T min, T max) => new(
        T.Clamp(value.X, min, max),
        T.Clamp(value.Y, min, max)
    );
    public static Vector2<T> SphereClamp(Vector2<T> value, T minLength, T maxLength) {
        var squaredLength = value.SquaredLength;
        if (squaredLength < minLength * minLength) return value.Normalized() * minLength;
        if (squaredLength > maxLength * maxLength) return value.Normalized() * maxLength;
        return value;
    }
    public static Vector2<T> Lerp(Vector2<T> a, Vector2<T> b, T t) => new(
        MathG.Lerp(a.X, b.X, t),
        MathG.Lerp(a.Y, b.Y, t)
    );

    #endregion
}