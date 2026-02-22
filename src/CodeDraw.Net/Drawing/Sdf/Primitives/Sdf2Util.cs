using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;

internal static class Sdf2Util
{
    public const float EPS = 1e-8f;

    public static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var denom = Vector2.Dot(ab, ab);
        if (denom <= EPS) return (p - a).Length;

        var t = Vector2.Dot(p - a, ab) / denom;
        t = MathG.Max(0f, MathG.Min(1f, t));
        var q = a + ab * t;
        return (p - q).Length;
    }

    public static float DistToSegSigned(Vector2 p, Vector2 a, Vector2 b, float radius)
        => DistToSeg(p, a, b) - radius;

    public static float BoxSdf(Rect r, Vector2 p)
    {
        // Standard axis-aligned box SDF (Inigo Quilez style)
        var c = r.Center;
        var e = new Vector2(r.Width * 0.5f, r.Height * 0.5f);

        var d = Vector2.Abs(p - c) - e;
        var outside = Vector2.Max(d, Vector2.Zero).Length;
        var inside = MathG.Min(MathG.Max(d.X, d.Y), 0f);
        return outside + inside;
    }

    public static float RoundedBoxSdf(Rect r, Vector2 p, float radius)
    {
        // Rounded axis-aligned rectangle SDF
        var rad = MathG.Max(0f, radius);
        rad = MathG.Min(rad, 0.5f * MathG.Min(r.Width, r.Height)); // clamp so it can't invert the box

        var c = r.Center;
        var e = new Vector2(r.Width * 0.5f - rad, r.Height * 0.5f - rad);

        var d = Vector2.Abs(p - c) - e;
        var outside = Vector2.Max(d, Vector2.Zero).Length;
        var inside = MathG.Min(MathG.Max(d.X, d.Y), 0f);
        return outside + inside - rad;
    }
}