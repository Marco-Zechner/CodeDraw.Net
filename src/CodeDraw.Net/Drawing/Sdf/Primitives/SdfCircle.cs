using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;

public readonly record struct SdfCircle(Vector2 Center, float Radius) : ISdf2
{
    public float DistanceLocal(Vector2 pLocal)
        => (pLocal - Center).Length - Radius;

    public Rect LocalBounds
        => new Rect(Center, new Vector2(Radius * 2f, Radius * 2f), OriginLocating.Center);
}