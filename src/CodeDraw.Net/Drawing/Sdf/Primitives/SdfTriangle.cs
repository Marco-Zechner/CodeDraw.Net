using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;

public readonly record struct SdfTriangle(Vector2 A, Vector2 B, Vector2 C) : ISdf2
{
    public float DistanceLocal(Vector2 p)
    {
        // Distance to triangle edges + inside test
        var d0 = Sdf2Util.DistToSeg(p, A, B);
        var d1 = Sdf2Util.DistToSeg(p, B, C);
        var d2 = Sdf2Util.DistToSeg(p, C, A);
        var d = MathG.Min(d0, MathG.Min(d1, d2));

        return PointInTriangle(p, A, B, C) ? -d : d;
    }

    public Rect LocalBounds
    {
        get
        {
            var minX = MathG.Min(A.X, MathG.Min(B.X, C.X));
            var minY = MathG.Min(A.Y, MathG.Min(B.Y, C.Y));
            var maxX = MathG.Max(A.X, MathG.Max(B.X, C.X));
            var maxY = MathG.Max(A.Y, MathG.Max(B.Y, C.Y));
            return new Rect(minX, minY, maxX, maxY);
        }
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        // barycentric sign test
        var ab = b - a; var ap = p - a;
        var bc = c - b; var bp = p - b;
        var ca = a - c; var cp = p - c;

        var c1 = Cross(ab, ap);
        var c2 = Cross(bc, bp);
        var c3 = Cross(ca, cp);

        var hasNeg = c1 < 0 || c2 < 0 || c3 < 0;
        var hasPos = c1 > 0 || c2 > 0 || c3 > 0;
        return !(hasNeg && hasPos);
    }

    private static float Cross(Vector2 u, Vector2 v) => u.X * v.Y - u.Y * v.X;
}