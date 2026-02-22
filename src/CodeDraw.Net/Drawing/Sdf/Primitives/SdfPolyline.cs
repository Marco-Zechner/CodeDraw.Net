using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;

public sealed class SdfPolyline : ISdf2
{
    private readonly Vector2[] _pts;
    private readonly bool _closed;
    private readonly Rect _bounds;

    public SdfPolyline(ReadOnlySpan<Vector2> pts, bool closed)
    {
        if (pts.Length < 2) throw new ArgumentException("Polyline needs >= 2 points.", nameof(pts));
        _pts = pts.ToArray();
        _closed = closed;

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
        var d = float.PositiveInfinity;
        for (var i = 1; i < _pts.Length; i++)
            d = MathG.Min(d, Sdf2Util.DistToSeg(p, _pts[i - 1], _pts[i]));

        if (_closed)
            d = MathG.Min(d, Sdf2Util.DistToSeg(p, _pts[^1], _pts[0]));

        return d; // stroke-only: unsigned distance is intended
    }
}