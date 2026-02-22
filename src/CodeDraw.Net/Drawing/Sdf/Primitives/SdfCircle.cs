using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;

public readonly record struct SdfCircle(Vector2 Center, float Radius) : ISdf2
{
    public float DistanceLocal(Vector2 p) => (p - Center).Length - Radius;

    public Rect LocalBounds
        => new RectBounds(
            Center.X - Radius, Center.Y - Radius,
            Center.X + Radius, Center.Y + Radius
        );
}