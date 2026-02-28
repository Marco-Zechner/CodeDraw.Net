using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

internal readonly record struct SdfPlaced
{
    public SdfPlaced(ISdf2Node rootNode, ISdf2 shape, Matrix3x3 localToWorld)
    {
        RootNode = rootNode;
        Shape = shape;
        LocalToWorld = localToWorld;

        var lb = shape.LocalBounds;

        var p0 = Matrix3x3.TransformAffine(LocalToWorld, new Vector2(lb.Left,  lb.Top));
        var p1 = Matrix3x3.TransformAffine(LocalToWorld, new Vector2(lb.Right, lb.Top));
        var p2 = Matrix3x3.TransformAffine(LocalToWorld, new Vector2(lb.Right, lb.Bottom));
        var p3 = Matrix3x3.TransformAffine(LocalToWorld, new Vector2(lb.Left,  lb.Bottom));

        var minX = MathG.Min(MathG.Min(p0.X, p1.X), MathG.Min(p2.X, p3.X));
        var minY = MathG.Min(MathG.Min(p0.Y, p1.Y), MathG.Min(p2.Y, p3.Y));
        var maxX = MathG.Max(MathG.Max(p0.X, p1.X), MathG.Max(p2.X, p3.X));
        var maxY = MathG.Max(MathG.Max(p0.Y, p1.Y), MathG.Max(p2.Y, p3.Y));

        WorldBounds = Rect.FromMinMaxUnchecked(new Vector2(minX, minY), new Vector2(maxX, maxY));
    }

    public Rect WorldBounds { get; }
    public ISdf2 Shape { get; }
    public ISdf2Node RootNode { get; }           // <-- needed for material walk
    public Matrix3x3 LocalToWorld { get; init; }

    public bool TryGetWorldToLocal(out Matrix3x3 w2L)
        => Matrix3x3.TryInvert(LocalToWorld, out w2L);

    public void Deconstruct(out ISdf2Node rootNode, out ISdf2 shape, out Matrix3x3 localToWorld)
    {
        rootNode = RootNode;
        shape = Shape;
        localToWorld = LocalToWorld;
    }
}