using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;

public readonly record struct SdfSegment(Vector2 A, Vector2 B, float Radius = 0f) : ISdf2
{
    public float DistanceLocal(Vector2 p)
        => Sdf2Util.DistToSegSigned(p, A, B, Radius);

    public Rect LocalBounds
    {
        get
        {
            var r = MathG.Max(0f, Radius);
            var min = new Vector2(MathG.Min(A.X, B.X) - r, MathG.Min(A.Y, B.Y) - r);
            var max = new Vector2(MathG.Max(A.X, B.X) + r, MathG.Max(A.Y, B.Y) + r);
            return Rect.FromMinMaxUnchecked(min, max);
        }
    }
}