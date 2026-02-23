using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;

public sealed class SdfPolygon : ISdf2
{
    private readonly Vector2[] _pts;
    internal ReadOnlySpan<Vector2> Points => _pts;
    private readonly Rect _bounds;

    public SdfPolygon(ReadOnlySpan<Vector2> pts)
    {
        if (pts.Length < 3) throw new ArgumentException("Polygon needs >= 3 points.", nameof(pts));
        _pts = pts.ToArray();

        float minX = _pts[0].X, minY = _pts[0].Y, maxX = _pts[0].X, maxY = _pts[0].Y;
        for (var i = 1; i < _pts.Length; i++)
        {
            var p = _pts[i];
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }
        _bounds = new RectBounds(minX, minY, maxX, maxY);
    }

    public Rect LocalBounds => _bounds;

    public float DistanceLocal(Vector2 p)
    {
        // min distance to edges
        var d = float.PositiveInfinity;
        for (int i = 0, j = _pts.Length - 1; i < _pts.Length; j = i++)
            d = MathG.Min(d, Sdf2Util.DistToSeg(p, _pts[j], _pts[i]));

        // inside test (ray casting)
        var inside = false;
        for (int i = 0, j = _pts.Length - 1; i < _pts.Length; j = i++)
        {
            var a = _pts[i];
            var b = _pts[j];

            // check edge crosses horizontal ray at p.Y
            var cond = (a.Y > p.Y) != (b.Y > p.Y);
            if (!cond) continue;

            var denom = (b.Y - a.Y);
            if (Math.Abs(denom) < Sdf2Util.EPS) continue;

            var xHit = (b.X - a.X) * (p.Y - a.Y) / denom + a.X;
            if (p.X < xHit) inside = !inside;
        }

        return inside ? -d : d;
    }
}