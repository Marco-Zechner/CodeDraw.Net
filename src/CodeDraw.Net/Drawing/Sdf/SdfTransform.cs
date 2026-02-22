using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

internal readonly record struct SdfTransform(ISdf2 Child, Matrix3x3 LocalToParent) : ISdf2
{
    private readonly Matrix3x3 _p2C = TryInvert(LocalToParent);

    public float DistanceLocal(Vector2 pParent)
    {
        // parent -> child
        var pChild = Matrix3x3.TransformAffine(_p2C, pParent);
        return Child.DistanceLocal(pChild);
    }

    public Rect LocalBounds
    {
        get
        {
            // Child bounds are in child space; map into parent space using LocalToParent.
            var b = Child.LocalBounds;

            var p0 = Matrix3x3.TransformAffine(LocalToParent, new Vector2(b.Left,  b.Top));
            var p1 = Matrix3x3.TransformAffine(LocalToParent, new Vector2(b.Right, b.Top));
            var p2 = Matrix3x3.TransformAffine(LocalToParent, new Vector2(b.Right, b.Bottom));
            var p3 = Matrix3x3.TransformAffine(LocalToParent, new Vector2(b.Left,  b.Bottom));

            var minX = MathG.Min(MathG.Min(p0.X, p1.X), MathG.Min(p2.X, p3.X));
            var minY = MathG.Min(MathG.Min(p0.Y, p1.Y), MathG.Min(p2.Y, p3.Y));
            var maxX = MathG.Max(MathG.Max(p0.X, p1.X), MathG.Max(p2.X, p3.X));
            var maxY = MathG.Max(MathG.Max(p0.Y, p1.Y), MathG.Max(p2.Y, p3.Y));

            return Rect.FromMinMaxUnchecked(new Vector2(minX, minY), new Vector2(maxX, maxY));
        }
    }

    private static Matrix3x3 TryInvert(in Matrix3x3 m)
    {
        if (!Matrix3x3.TryInvert(m, out var inv))
            throw new InvalidOperationException("SdfTransform requires an invertible matrix.");
        return inv;
    }
}