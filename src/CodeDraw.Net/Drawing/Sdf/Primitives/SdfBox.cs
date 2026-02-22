using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;

public readonly record struct SdfBox(Rect LocalRectTopLeftNormalized) : ISdf2
{
    public float DistanceLocal(Vector2 p)
    {
        // Standard axis-aligned box SDF in local space.
        var r = LocalRectTopLeftNormalized.NormalizedTopLeft();
        var c = r.Center;
        var e = new Vector2(r.Width * 0.5f, r.Height * 0.5f);

        var d = Vector2.Abs(p - c) - e;
        var outside = Vector2.Max(d, Vector2.Zero).Length;
        var inside = MathG.Min(MathG.Max(d.X, d.Y), 0f);
        return outside + inside;
    }

    public Rect LocalBounds => LocalRectTopLeftNormalized.NormalizedTopLeft();
}