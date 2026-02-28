using System.Runtime.InteropServices;
using MarcoZechner.ColorDotNet.RGB;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;
using MarcoZechner.CodeDrawDotNet.DrawLayer;
using MarcoZechner.CodeDrawDotNet.DrawLayer.Commands;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing;

internal sealed class PathBuilderImpl(CodeDrawLayer layer, in Matrix3x3 xf, in DrawStyle style) : IPathBuilder
{
    private readonly Matrix3x3 _xfAtStart = xf;

    private DrawStyle _style = style;
    private Paint? _paintOverride;
    private Stroke? _strokeOverride;
    private ColorF? _fillOverride;

    private readonly List<Vector2> _pts = new();
    private bool _hasCurrent;
    private bool _closed;

    public IPathBuilder MoveTo(float x, float y)
    {
        _pts.Clear();
        _pts.Add(new Vector2(x, y));
        _hasCurrent = true;
        _closed = false;
        return this;
    }

    public IPathBuilder LineTo(float x, float y)
    {
        if (!_hasCurrent) return MoveTo(x, y);
        _pts.Add(new Vector2(x, y));
        return this;
    }

    public IPathBuilder QuadTo(float cx, float cy, float x, float y)
    {
        if (!_hasCurrent) return MoveTo(x, y);

        var p0 = _pts[^1];
        var p1 = new Vector2(cx, cy);
        var p2 = new Vector2(x, y);

        const int N = 16;
        for (var i = 1; i <= N; i++)
        {
            var t = i / (float)N;
            var a = Vector2.Lerp(p0, p1, t);
            var b = Vector2.Lerp(p1, p2, t);
            var q = Vector2.Lerp(a, b, t);
            _pts.Add(q);
        }
        return this;
    }

    public IPathBuilder CubicTo(float cx1, float cy1, float cx2, float cy2, float x, float y)
    {
        if (!_hasCurrent) return MoveTo(x, y);

        var p0 = _pts[^1];
        var p1 = new Vector2(cx1, cy1);
        var p2 = new Vector2(cx2, cy2);
        var p3 = new Vector2(x, y);

        const int N = 24;
        for (var i = 1; i <= N; i++)
        {
            var t = i / (float)N;
            var a = Vector2.Lerp(p0, p1, t);
            var b = Vector2.Lerp(p1, p2, t);
            var c = Vector2.Lerp(p2, p3, t);
            var d = Vector2.Lerp(a, b, t);
            var e = Vector2.Lerp(b, c, t);
            var q = Vector2.Lerp(d, e, t);
            _pts.Add(q);
        }
        return this;
    }

    public IPathBuilder ArcTo(float cx, float cy, float radius, float startDeg, float sweepDeg, int segmentsHint = 0)
    {
        if (!_hasCurrent)
        {
            var rad0 = startDeg * MathF.PI / 180f;
            MoveTo(cx + MathF.Cos(rad0) * radius, cy + MathF.Sin(rad0) * radius);
        }

        var segs = segmentsHint > 0 ? segmentsHint : Math.Max(6, (int)(MathF.Abs(sweepDeg) / 10f));
        var start = startDeg * MathF.PI / 180f;
        var sweep = sweepDeg * MathF.PI / 180f;

        for (var i = 1; i <= segs; i++)
        {
            var t = i / (float)segs;
            var a = start + sweep * t;
            LineTo(cx + MathF.Cos(a) * radius, cy + MathF.Sin(a) * radius);
        }
        return this;
    }

    public IPathBuilder Close() { _closed = true; return this; }

    public IPathBuilder Fill(ColorF fill) { _fillOverride = fill; return this; }
    public IPathBuilder Stroke(in Stroke stroke) { _strokeOverride = stroke; return this; }
    public IPathBuilder Paint(in Paint paint) { _paintOverride = paint; return this; }
    public IPathBuilder Style(in DrawStyle style) { _style = style; return this; }

    public void Draw()
    {
        if (TryBuildCmd(out var cmd))
            layer.Enqueue(cmd);
    }

    internal bool TryBuildCmd(out CmdSdf cmd)
    {
        cmd = default;
        if (_pts.Count < 2) return false;

        var paint = _style.Paint;

        if (_paintOverride.HasValue) paint = _paintOverride.Value;
        if (_fillOverride.HasValue) paint = paint with { Fill = _fillOverride.Value };
        if (_strokeOverride.HasValue) paint = paint with { Stroke = _strokeOverride.Value };

        var style = _style with { Paint = paint };

        // Transitional: leaf material derived from style.
        var mat = new SdfMaterial(style, SdfColorOverwrite.OnlyDefault);

        if (_closed && _pts.Count >= 3)
        {
            var prim = new SdfPolygon(CollectionsMarshal.AsSpan(_pts));
            cmd = new CmdSdf(
                Placed: SdfPlacedFactory.FromPrimitive(prim, _xfAtStart, mat),
                Style: style
            );
            return true;
        }
        else
        {
            var prim = new SdfPolyline(CollectionsMarshal.AsSpan(_pts), closed: false, radius: 0f);
            cmd = new CmdSdf(
                Placed: SdfPlacedFactory.FromPrimitive(prim, _xfAtStart, mat),
                Style: style,
                ForceStrokeOnly: true
            );
            return true;
        }
    }
}