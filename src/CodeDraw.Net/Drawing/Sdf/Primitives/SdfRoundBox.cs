using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;

public readonly record struct SdfRoundBox(Rect LocalRectTopLeftNormalized, float Radius) : ISdf2
{
    public float DistanceLocal(Vector2 p)
    {
        var r = LocalRectTopLeftNormalized.NormalizedTopLeft();
        var c = r.Center;
        var rad = MathG.Min(Radius, 0.5f * MathG.Min(r.Width, r.Height));
        var e = new Vector2(r.Width * 0.5f - rad, r.Height * 0.5f - rad);

        var d = Vector2.Abs(p - c) - e;
        var outside = Vector2.Max(d, Vector2.Zero).Length;
        var inside = MathG.Min(MathG.Max(d.X, d.Y), 0f);
        return outside + inside - rad;
    }

    public Rect LocalBounds => LocalRectTopLeftNormalized.NormalizedTopLeft();
}