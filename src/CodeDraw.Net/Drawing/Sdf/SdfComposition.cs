using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

internal readonly record struct SdfUnion(ISdf2 A, ISdf2 B) : ISdf2
{
    public float DistanceLocal(Vector2 p) => MathF.Min(A.DistanceLocal(p), B.DistanceLocal(p));
    public Rect LocalBounds => A.LocalBounds.Union(B.LocalBounds);
}

internal readonly record struct SdfIntersect(ISdf2 A, ISdf2 B) : ISdf2
{
    public float DistanceLocal(Vector2 p) => MathF.Max(A.DistanceLocal(p), B.DistanceLocal(p));
    public Rect LocalBounds => A.LocalBounds.Intersection(B.LocalBounds);
}

internal readonly record struct SdfSubtract(ISdf2 A, ISdf2 B) : ISdf2
{
    // A minus B
    public float DistanceLocal(Vector2 p) => MathF.Max(A.DistanceLocal(p), -B.DistanceLocal(p));
    public Rect LocalBounds => A.LocalBounds; // conservative
}

internal readonly record struct SdfSmoothUnion(ISdf2 A, ISdf2 B, float K) : ISdf2
{
    public float DistanceLocal(Vector2 p)
    {
        var da = A.DistanceLocal(p);
        var db = B.DistanceLocal(p);
        return SdfMath.SmoothMin(da, db, K);
    }

    public Rect LocalBounds => A.LocalBounds.Union(B.LocalBounds);
}