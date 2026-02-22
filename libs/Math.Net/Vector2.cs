using System.Numerics;

namespace MarcoZechner.MathDotNet;

/// <summary>
/// A 2D vector using float components (default).
/// Hosts the "main" static API surface as well (Distance/Dot/Cross/...).
/// </summary>
public readonly partial record struct Vector2(float X, float Y)
{
#region Generic Interaction

    public Vector2<float> AsGeneric() => new(X, Y);

    public void Deconstruct(out float x, out float y) => (x, y) = (X, Y);

#endregion

#region Conversions
    
    public static explicit operator Vector2(Vector2<double> v) => new((float)v.X, (float)v.Y);
    public static implicit operator Vector2<double>(Vector2 v) => new(v.X, v.Y);

    public static implicit operator Vector2(Vector2<float> v) => new(v.X, v.Y);
    public static implicit operator Vector2<float>(Vector2 v) => new(v.X, v.Y);

    public static implicit operator Vector2(Vector2<int> v) => new(v.X, v.Y);
    public static explicit operator Vector2<int>(Vector2 v) => new((int)v.X, (int)v.Y);

#endregion

#region ValueTuple support
    
    public static explicit operator Vector2((double x, double y) t) => new((float)t.x, (float)t.y);
    public static implicit operator (double x, double y)(Vector2 v) => (v.X, v.Y);
    
    public static implicit operator Vector2((float x, float y) t) => new(t.x, t.y);
    public static implicit operator (float x, float y)(Vector2 v) => (v.X, v.Y);

    public static implicit operator Vector2((int x, int y) t) => new(t.x, t.y);
    public static explicit operator (int x, int y)(Vector2 v) => ((int)v.X, (int)v.Y);
    
    public static (int x, int y) ToIntValueTuple(Vector2 v) => ((int)v.X, (int)v.Y);

#endregion

#region Tuple support
    
    public static explicit operator Vector2(Tuple<double, double> t) => new((float)t.Item1, (float)t.Item2);
    public static implicit operator Tuple<double, double>(Vector2 v) => new(v.X, v.Y);
    
    public static implicit operator Vector2(Tuple<float, float> t) => new(t.Item1, t.Item2);
    public static implicit operator Tuple<float, float>(Vector2 v) => new(v.X, v.Y);

    public static implicit operator Vector2(Tuple<int, int> t) => new(t.Item1, t.Item2);
    public static explicit operator Tuple<int, int>(Vector2 v) => new((int)v.X, (int)v.Y);

#endregion

#region IndexAccess
    
    public float this[int i]
    {
        get => i switch
        {
            0 => X,
            1 => Y,
            _ => throw new IndexOutOfRangeException("Vector2 only has indices 0 and 1.")
        };
        init
        {
            switch (i)
            {
                case 0: X = value; break;
                case 1: Y = value; break;
                default: throw new IndexOutOfRangeException("Vector2 only has indices 0 and 1.");
            }
        }
    }

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

    public float SquaredLength => X * X + Y * Y;
    public float Length => float.Sqrt(SquaredLength);

    public Vector2 Normalized
    {
        get
        {
            var len = Length;
            return len == 0f ? Zero : new(X / len, Y / len);
        }
    }
    
    public Vector2 Abs => new(MathG.Abs(X), MathG.Abs(Y));

#endregion
    public override string ToString() => $"Vector2({X}, {Y})";
        
    public Vector2 Scalar(float s) => new(X * s, Y * s);

    
#region Binary Operators (Vector-Vector)

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator *(Vector2 a, Vector2 b) => new(a.X * b.X, a.Y * b.Y);
    public static Vector2 operator /(Vector2 a, Vector2 b) => new(a.X / b.X, a.Y / b.Y);

#endregion

#region Binary Operators (Vector-Scalar)
    
    public static Vector2 operator *(Vector2 a, float s) => new(a.X * s, a.Y * s);
    public static Vector2 operator *(float s, Vector2 a) => new(a.X * s, a.Y * s);

    public static Vector2 operator /(Vector2 a, float s) => new(a.X / s, a.Y / s);
    public static Vector2 operator /(float s, Vector2 a) => new(s / a.X, s / a.Y);

    public static Vector2 operator %(Vector2 a, float s) => new(a.X % s, a.Y % s);
    public static Vector2 operator %(float s, Vector2 a) => new(s % a.X, s % a.Y);
    
#endregion

#region Unary Operators
    
    public static Vector2 operator -(Vector2 a) => new(-a.X, -a.Y);

#endregion
    
#region Cross-type scalar ops as instance methods
    
    public Vector2<TOut> Scalar<TOut, TScalar>(TScalar s)
        where TOut : unmanaged, INumber<TOut>
        where TScalar : INumber<TScalar>
        => AsGeneric().Scalar<TOut, TScalar>(s);

    public Vector2<TOut> Scalar<TOut>(double s)
        where TOut : unmanaged, INumber<TOut>
        => AsGeneric().Scalar<TOut>(s);

    public Vector2<TOut> Divide<TOut, TScalar>(TScalar s)
        where TOut : unmanaged, INumber<TOut>
        where TScalar : INumber<TScalar>
        => AsGeneric().Divide<TOut, TScalar>(s);

    public Vector2<TOut> Divide<TOut>(double s)
        where TOut : unmanaged, INumber<TOut>
        => AsGeneric().Divide<TOut>(s);

    
#endregion
}