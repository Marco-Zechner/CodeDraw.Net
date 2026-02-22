using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;

public readonly record struct SdfRoundedRect(Rect R, float Radius) : ISdf2
{
    public float DistanceLocal(Vector2 p) => Sdf2Util.RoundedBoxSdf(R, p, Radius);
    public Rect LocalBounds => R.Expand(Radius);
}