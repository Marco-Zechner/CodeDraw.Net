using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;

public readonly record struct SdfSegment(Vector2 A, Vector2 B, float Radius = 0f) : ISdf2
{
    public float DistanceLocal(Vector2 p)
    {
        // Distance to segment with optional radius (capsule).
        var pa = p - A;
        var ba = B - A;
        var denom = Vector2.Dot(ba, ba);
        if (denom <= 0f) return (p - A).Length - Radius;
        var h = MathG.Clamp(Vector2.Dot(pa, ba) / denom, 0f, 1f);
        var closest = A + ba * h;
        return (p - closest).Length - Radius;
    }

    public Rect LocalBounds
    {
        get
        {
            var min = new Vector2(MathG.Min(A.X, B.X) - Radius, MathG.Min(A.Y, B.Y) - Radius);
            var max = new Vector2(MathG.Max(A.X, B.X) + Radius, MathG.Max(A.Y, B.Y) + Radius);
            return Rect.FromMinMaxUnchecked(min, max);
        }
    }
}