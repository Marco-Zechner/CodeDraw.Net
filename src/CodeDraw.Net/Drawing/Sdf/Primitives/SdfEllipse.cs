using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;

public readonly record struct SdfEllipse(Vector2 Center, Vector2 Radius) : ISdf2
{
    public float DistanceLocal(Vector2 p)
    {
        // Approx ellipse SDF (stable, not exact)
        var rx = MathG.Max(Sdf2Util.EPS, Radius.X);
        var ry = MathG.Max(Sdf2Util.EPS, Radius.Y);
        var q = new Vector2((p.X - Center.X) / rx, (p.Y - Center.Y) / ry);
        return q.Length - 1f;
    }

    public Rect LocalBounds
        => new Rect(
            Center.X - Radius.X, Center.Y - Radius.Y,
            Center.X + Radius.X, Center.Y + Radius.Y
        );
}