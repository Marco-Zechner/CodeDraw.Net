using Silk.NET.Maths;

namespace MarcoZechner.Math;

public readonly partial record struct Vector2{
    public static implicit operator Vector2D<float>(Vector2 v) => new(v.X, v.Y);
    public static implicit operator Vector2(Vector2D<float> v) => new(v.X, v.Y);

    public static implicit operator Vector2D<double>(Vector2 v) => new(v.X, v.Y);
    public static implicit operator Vector2(Vector2D<double> v) => new((float)v.X, (float)v.Y);

    public static implicit operator Vector2D<int>(Vector2 v) => new((int)v.X, (int)v.Y);
    public static implicit operator Vector2(Vector2D<int> v) => new(v.X, v.Y);
}

public readonly partial record struct Vector2<T>
{
    public static implicit operator Vector2D<T>(Vector2<T> v) => new(v.X, v.Y);
    public static implicit operator Vector2<T>(Vector2D<T> v) => new(v.X, v.Y);    
}