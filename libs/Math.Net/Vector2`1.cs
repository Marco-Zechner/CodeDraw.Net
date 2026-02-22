using System.Numerics;

namespace MarcoZechner.MathDotNet;

public readonly partial record struct Vector2<T>(T X, T Y) where T : unmanaged, INumber<T>
{
    public Vector2(Vector2<T> v) : this(v.X, v.Y) { }

    public void Deconstruct(out T x, out T y) { x = X; y = Y; }

#region Implicit/Explicit Conversions
    
    public static implicit operator Vector2<double>(Vector2<T> v) => new(MathG.ToDouble(v.X), MathG.ToDouble(v.Y));

    public static explicit operator Vector2<float>(Vector2<T> v) => new(MathG.ToFloat(v.X), MathG.ToFloat(v.Y));

    public static explicit operator Vector2<int>(Vector2<T> v) => new(int.CreateChecked(v.X), int.CreateChecked(v.Y));
    
#endregion 
    
#region ValueTuple support
    
    public static explicit operator Vector2<T>((double x, double y) t) => new(MathG.FromDouble<T>(t.x), MathG.FromDouble<T>(t.y));
    public static implicit operator (double x, double y)(Vector2<T> v) => (MathG.ToDouble(v.X), MathG.ToDouble(v.Y));
    
    public static explicit operator Vector2<T>((float x, float y) t) => new(MathG.FromFloat<T>(t.x), MathG.FromFloat<T>(t.y));
    public static explicit operator (float x, float y)(Vector2<T> v) => (MathG.ToFloat(v.X), MathG.ToFloat(v.Y));
    
    public static implicit operator Vector2<T>((int x, int y) t) => new(T.CreateChecked(t.x), T.CreateChecked(t.y));
    public static explicit operator (int x, int y)(Vector2<T> v) => (int.CreateChecked(v.X), int.CreateChecked(v.Y));
    
#endregion

#region Tuple support
    
    public static explicit operator Vector2<T>(Tuple<double, double> t) => new(MathG.FromDouble<T>(t.Item1), MathG.FromDouble<T>(t.Item2));
    public static implicit operator Tuple<double, double>(Vector2<T> v) => new(MathG.ToDouble(v.X), MathG.ToDouble(v.Y));
    
    public static explicit operator Vector2<T>(Tuple<float, float> t) => new(MathG.FromFloat<T>(t.Item1), MathG.FromFloat<T>(t.Item2));
    public static explicit operator Tuple<float, float>(Vector2<T> v) => new(MathG.ToFloat(v.X), MathG.ToFloat(v.Y));
    
    public static explicit operator Vector2<T>(Tuple<int, int> t) => new(T.CreateChecked(t.Item1), T.CreateChecked(t.Item2));
    public static explicit operator Tuple<int, int>(Vector2<T> v) => new(int.CreateChecked(v.X), int.CreateChecked(v.Y));

#endregion

#region IndexAccess
    
    public T this[int i]
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
    
    public static Vector2<T> Zero => new(T.Zero, T.Zero);
    public static Vector2<T> One => new(T.One, T.One);
    public static Vector2<T> Up => new(T.Zero, T.One);
    public static Vector2<T> Down => new(T.Zero, -T.One);
    public static Vector2<T> Left => new(-T.One, T.Zero);
    public static Vector2<T> Right => new(T.One, T.Zero);
    
#endregion

#region Length / Normalize (pattern: *F and *<TOut>)

    public T SquaredLength => X * X + Y * Y;

    public float LengthF => MathG.SqrtF(SquaredLength);

    public TOut Length<TOut>() where TOut : unmanaged, INumber<TOut>
        => MathG.FromDouble<TOut>(Math.Sqrt(MathG.ToDouble(SquaredLength)));

    public Vector2<float> NormalizedF
    {
        get
        {
            var len = LengthF;
            if (len == 0f) return Vector2<float>.Zero;
            return new Vector2<float>(MathG.ToFloat(X) / len, MathG.ToFloat(Y) / len);
        }
    }

    public Vector2<TOut> Normalized<TOut>() where TOut : unmanaged, INumber<TOut>
    {
        var len = Length<double>();
        if (len == 0.0) return Vector2<TOut>.Zero;

        var dx = MathG.ToDouble(X) / len;
        var dy = MathG.ToDouble(Y) / len;
        return new Vector2<TOut>(MathG.FromDouble<TOut>(dx), MathG.FromDouble<TOut>(dy));
    }

#endregion

    public override string ToString() => $"Vector2<{typeof(T).Name}>({X}, {Y})";

    
#region Binary Operators (Vector-Vector)

    public static Vector2<T> operator +(Vector2<T> a, Vector2<T> b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2<T> operator -(Vector2<T> a, Vector2<T> b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>Component-wise multiply (Hadamard).</summary>
    public static Vector2<T> operator *(Vector2<T> a, Vector2<T> b) => new(a.X * b.X, a.Y * b.Y);

    public static Vector2<T> operator /(Vector2<T> a, Vector2<T> b)
    {
        if (b.X == T.Zero || b.Y == T.Zero)
            throw new DivideByZeroException("Cannot divide by zero in Vector2 component division.");
        return new Vector2<T>(a.X / b.X, a.Y / b.Y);
    }

#endregion

#region Binary Operators (Vector-Scalar)

    public static Vector2<T> operator *(Vector2<T> a, T scalar) => new(a.X * scalar, a.Y * scalar);
    public static Vector2<T> operator *(T scalar, Vector2<T> a) => new(a.X * scalar, a.Y * scalar);

    public static Vector2<T> operator /(Vector2<T> a, T scalar)
    {
        if (scalar == T.Zero) throw new DivideByZeroException("Cannot divide by zero in Vector2 scalar division.");
        return new Vector2<T>(a.X / scalar, a.Y / scalar);
    }
    public static Vector2<T> operator /(T scalar, Vector2<T> a)
    {
        if (a.X == T.Zero || a.Y == T.Zero)
            throw new DivideByZeroException("Cannot divide by zero in scalar/vector division.");
        return new Vector2<T>(scalar / a.X, scalar / a.Y);
    }

    public static Vector2<T> operator %(Vector2<T> a, T scalar)
    {
        if (scalar == T.Zero) throw new DivideByZeroException("Cannot modulo by zero in Vector2 scalar modulo.");
        return new Vector2<T>(a.X % scalar, a.Y % scalar);
    }
    public static Vector2<T> operator %(T scalar, Vector2<T> a)
    {
        if (a.X == T.Zero || a.Y == T.Zero)
            throw new DivideByZeroException("Cannot modulo by zero in scalar/vector modulo.");
        return new Vector2<T>(scalar % a.X, scalar % a.Y);
    }

#endregion

#region Unary Operators

    public static Vector2<T> operator -(Vector2<T> a) => new(-a.X, -a.Y);

#endregion

#region Cross-type scalar ops as instance methods

    /// <summary>
    /// Multiply by any scalar type, choose output vector type explicitly.
    /// Example: Vector2&lt;int&gt; v2 = vFloat.Scalar&lt;int&gt;(5);
    /// </summary>
    public Vector2<TOut> Scalar<TOut, TScalar>(TScalar s)
        where TOut : unmanaged, INumber<TOut>
        where TScalar : INumber<TScalar>
    {
        var sx = MathG.ToDouble(X) * MathG.ToDouble(s);
        var sy = MathG.ToDouble(Y) * MathG.ToDouble(s);
        return new Vector2<TOut>(MathG.FromDouble<TOut>(sx), MathG.FromDouble<TOut>(sy));
    }

    /// <summary>Convenience: output type only, scalar inferred.</summary>
    public Vector2<TOut> Scalar<TOut>(double s)
        where TOut : unmanaged, INumber<TOut>
        => Scalar<TOut, double>(s);

    /// <summary>
    /// Divide by any scalar type, choose output vector type explicitly.
    /// </summary>
    public Vector2<TOut> Divide<TOut, TScalar>(TScalar s)
        where TOut : unmanaged, INumber<TOut>
        where TScalar : INumber<TScalar>
    {
        var ds = MathG.ToDouble(s);
        if (ds == 0.0) throw new DivideByZeroException("Cannot divide by zero.");
        var dx = MathG.ToDouble(X) / ds;
        var dy = MathG.ToDouble(Y) / ds;
        return new Vector2<TOut>(MathG.FromDouble<TOut>(dx), MathG.FromDouble<TOut>(dy));
    }

    public Vector2<TOut> Divide<TOut>(double s)
        where TOut : unmanaged, INumber<TOut>
        => Divide<TOut, double>(s);

    #endregion
}