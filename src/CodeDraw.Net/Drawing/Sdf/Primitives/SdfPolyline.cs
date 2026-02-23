using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;

public sealed class SdfPolyline : ISdf2
{
    private readonly Vector2[] _pts;
    private readonly bool _closed;
    private readonly Rect _bounds;
    
    public float Radius { get; }
    
    internal ReadOnlySpan<Vector2> Points => _pts;
    internal bool Closed => _closed;

    public SdfPolyline(ReadOnlySpan<Vector2> pts, bool closed, float radius = 0f)
    {
        if (pts.Length < 2) throw new ArgumentException("Polyline needs >= 2 points.", nameof(pts));
        _pts = pts.ToArray();
        _closed = closed;
        Radius = MathF.Max(0f, radius);

        float minX = _pts[0].X, minY = _pts[0].Y, maxX = _pts[0].X, maxY = _pts[0].Y;
        for (var i = 1; i < _pts.Length; i++)
        {
            var p = _pts[i];
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        _bounds = new RectBounds(minX - Radius, minY - Radius, maxX + Radius, maxY + Radius);
    }

    public Rect LocalBounds => _bounds;

    public float DistanceLocal(Vector2 p)
    {
        var d = float.PositiveInfinity;
        for (var i = 1; i < _pts.Length; i++)
            d = MathG.Min(d, Sdf2Util.DistToSeg(p, _pts[i - 1], _pts[i]));

        if (_closed)
            d = MathG.Min(d, Sdf2Util.DistToSeg(p, _pts[^1], _pts[0]));

        return d - Radius; // now signed-ish band distance for thickness
    }
}