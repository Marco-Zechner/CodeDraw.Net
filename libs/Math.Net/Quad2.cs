namespace MarcoZechner.MathDotNet;

public readonly record struct Quad2(Vector2 P0, Vector2 P1, Vector2 P2, Vector2 P3)
{
    public Quad2((Vector2 P0, Vector2 P1, Vector2 P2, Vector2 P3) tuple) : this(tuple.P0, tuple.P1, tuple.P2, tuple.P3) { }
    
    public Quad2(float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3)
        : this(new Vector2(x0, y0), new Vector2(x1, y1), new Vector2(x2, y2), new Vector2(x3, y3)) { }
    
    public Rect Aabb
    {
        get
        {
            var minX = MathF.Min(MathF.Min(P0.X, P1.X), MathF.Min(P2.X, P3.X));
            var minY = MathF.Min(MathF.Min(P0.Y, P1.Y), MathF.Min(P2.Y, P3.Y));
            var maxX = MathF.Max(MathF.Max(P0.X, P1.X), MathF.Max(P2.X, P3.X));
            var maxY = MathF.Max(MathF.Max(P0.Y, P1.Y), MathF.Max(P2.Y, P3.Y));
            return Rect.FromMinMaxUnchecked(new Vector2(minX, minY), new Vector2(maxX, maxY));
        }
    }
}