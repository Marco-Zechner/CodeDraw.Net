using System.Numerics;

namespace MarcoZechner.MathDotNet;



public readonly record struct Quad2<T>(Vector2<T> P0, Vector2<T> P1, Vector2<T> P2, Vector2<T> P3)
    where T : unmanaged, INumber<T>
{
    public Quad2((Vector2<T> P0, Vector2<T> P1, Vector2<T> P2, Vector2<T> P3) tuple) : this(tuple.P0, tuple.P1, tuple.P2, tuple.P3) { }
    
    public Quad2(T x0, T y0, T x1, T y1, T x2, T y2, T x3, T y3)
        : this(new Vector2<T>(x0, y0), new Vector2<T>(x1, y1), new Vector2<T>(x2, y2), new Vector2<T>(x3, y3)) { }
    
    public Rect<T> Aabb
    {
        get
        {
            var minX = MathG.Min(MathG.Min(P0.X, P1.X), MathG.Min(P2.X, P3.X));
            var minY = MathG.Min(MathG.Min(P0.Y, P1.Y), MathG.Min(P2.Y, P3.Y));
            var maxX = MathG.Max(MathG.Max(P0.X, P1.X), MathG.Max(P2.X, P3.X));
            var maxY = MathG.Max(MathG.Max(P0.Y, P1.Y), MathG.Max(P2.Y, P3.Y));
            return Rect<T>.FromMinMaxUnchecked(new Vector2<T>(minX, minY), new Vector2<T>(maxX, maxY));
        }
    }
}