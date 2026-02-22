using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;

public readonly record struct SdfRect(Rect R) : ISdf2
{
    public float DistanceLocal(Vector2 p) => Sdf2Util.BoxSdf(R, p);
    public Rect LocalBounds => R;
}