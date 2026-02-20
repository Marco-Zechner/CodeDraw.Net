using System.Numerics;

namespace MarcoZechner.MathDotNet;

/// <summary>
/// A 2D vector using float components (default).
/// Hosts the "main" static API surface as well (Distance/Dot/Cross/...).
/// </summary>
public readonly partial record struct Vector2
{
#region Generic Interaction

    public Vector2<float> AsGeneric() => _v;
    private readonly Vector2<float> _v;
    public float X => _v.X;
    public float Y => _v.Y;
    public Vector2(float x, float y) : this(new(x, y)) { }
    
    private Vector2(Vector2<float> v) => _v = v;
    
    public Vector2 WithX(float x) => new(x, Y);
    public Vector2 WithY(float y) => new(X, y);
    
    public void Deconstruct(out float x, out float y) => (x, y) = (X, Y);

#endregion

#region Conversions
    
    public static implicit operator Vector2<double>(Vector2 v) => new(v.X, v.Y);
    public static explicit operator Vector2(Vector2<double> v) => new((float)v.X, (float)v.Y);

    public static implicit operator Vector2<float>(Vector2 v) => v._v;
    public static implicit operator Vector2(Vector2<float> v) => new(v);

    public static implicit operator Vector2<int>(Vector2 v) => new((int)v.X, (int)v.Y);
    public static explicit operator Vector2(Vector2<int> v) => new(v.X, v.Y);
    
#endregion

#region Constants
    
    public static Vector2 Zero => Vector2<float>.Zero;
    public static Vector2 One => Vector2<float>.One;
    public static Vector2 Up => Vector2<float>.Up;
    public static Vector2 Down => Vector2<float>.Down;
    public static Vector2 Left => Vector2<float>.Left;
    public static Vector2 Right => Vector2<float>.Right;
    
#endregion

#region Length / Normalize

    public float SquaredLength => _v.SquaredLength;
    public float Length => _v.LengthF;

    public Vector2 Normalized => _v.NormalizedF;

#endregion
    public override string ToString() => $"Vector2({X}, {Y})";
        
    public Vector2 Scalar(float s) => new(_v.Scalar<float>(s));

    
#region Binary Operators (Vector-Vector)

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a._v + b._v);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a._v - b._v);
    public static Vector2 operator *(Vector2 a, Vector2 b) => new(a._v * b._v);
    public static Vector2 operator /(Vector2 a, Vector2 b) => new(a._v / b._v);

#endregion

#region Binary Operators (Vector-Scalar)
    
    public static Vector2 operator *(Vector2 a, float s) => new(a._v * s);
    public static Vector2 operator *(float s, Vector2 a) => new(a._v * s);
    
    public static Vector2 operator /(Vector2 a, float s) => new(a._v / s);
    public static Vector2 operator /(float s, Vector2 a) => new(s / a._v.X, s / a._v.Y);

    public static Vector2 operator %(Vector2 a, float s) => new(a._v % s);
    public static Vector2 operator %(float s, Vector2 a) => new(s % a._v.X, s % a._v.Y);
    
#endregion

#region Unary Operators
    
    public static Vector2 operator -(Vector2 a) => new(-a._v.X, -a._v.Y);

#endregion
    
#region Cross-type scalar ops as instance methods
    
    public Vector2<TOut> Scalar<TOut, TScalar>(TScalar s) 
        where TOut : unmanaged, INumber<TOut> 
        where TScalar : INumber<TScalar>
        => _v.Scalar<TOut, TScalar>(s);
    
    public Vector2<TOut> Scalar<TOut>(double s) 
        where TOut : unmanaged, INumber<TOut>
        => _v.Scalar<TOut>(s);
    
    public Vector2<TOut> Divide<TOut, TScalar>(TScalar s) 
        where TOut : unmanaged, INumber<TOut> 
        where TScalar : INumber<TScalar>
        => _v.Divide<TOut, TScalar>(s);
    
    public Vector2<TOut> Divide<TOut>(double s) 
        where TOut : unmanaged, INumber<TOut>
        => _v.Divide<TOut>(s);
    
#endregion
}