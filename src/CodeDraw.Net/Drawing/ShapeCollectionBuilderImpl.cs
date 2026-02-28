using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;
using MarcoZechner.CodeDrawDotNet.DrawLayer;
using MarcoZechner.CodeDrawDotNet.DrawLayer.Commands;
using MarcoZechner.CodeDrawDotNet.Shaders;
using MarcoZechner.ColorDotNet.RGB;
using MarcoZechner.MathDotNet;

namespace MarcoZechner.CodeDrawDotNet.Drawing;

internal sealed class ShapeCollectionBuilderImpl(CodeDrawLayer layer, in Matrix3x3 initial) : IShapeCollectionBuilder
{
    private readonly CodeDrawLayer _layer = layer;
    private Matrix3x3 _xf = initial;
    private float _pivotAx, _pivotAy;
    private bool _hasPivot;

    private readonly List<ICmd> _commands = [];
    private CodeDrawShader? _shader;
    private Uniforms _uniforms;

    public IShapeCollectionBuilder Translate(float x, float y) { _xf *= Matrix3x3.CreateTranslation(x, y); return this; }
    public IShapeCollectionBuilder Scale(float sx, float sy) { _xf *= Matrix3x3.CreateScale(sx, sy); return this; }
    public IShapeCollectionBuilder RotateDeg(float deg) { _xf *= Matrix3x3.CreateRotation(deg); return this; }

    public IShapeCollectionBuilder RotateAround(float px, float py, float deg)
    {
        var t0 = Matrix3x3.CreateTranslation(px, py);
        var r = Matrix3x3.CreateRotation(deg);
        var t1 = Matrix3x3.CreateTranslation(-px, -py);
        _xf *= (t0 * r * t1);
        return this;
    }

    public IShapeCollectionBuilder Pivot(float ax, float ay)
    {
        _pivotAx = ax; _pivotAy = ay;
        _hasPivot = true;
        return this;
    }

    public IShapeCollectionBuilder AddRect(in Rect r, in Paint paint)
    {
        var style = new DrawStyle(paint);
        var mat = new SdfMaterial(style, SdfColorOverwrite.OnlyDefault);

        _commands.Add(new CmdSdf(
            Placed: SdfPlacedFactory.FromPrimitive(new SdfRect(r), _xf, mat),
            Style: style
        ));
        return this;
    }

    public IShapeCollectionBuilder AddCircle(float cx, float cy, float radius, in Paint paint)
    {
        var style = new DrawStyle(paint);
        var mat = new SdfMaterial(style, SdfColorOverwrite.OnlyDefault);

        _commands.Add(new CmdSdf(
            Placed: SdfPlacedFactory.FromPrimitive(new SdfCircle(new Vector2(cx, cy), radius), _xf, mat),
            Style: style
        ));
        return this;
    }

    public IShapeCollectionBuilder AddPolygon(ReadOnlySpan<Vector2> pts, in Paint paint)
    {
        var style = new DrawStyle(paint);
        var mat = new SdfMaterial(style, SdfColorOverwrite.OnlyDefault);

        _commands.Add(new CmdSdf(
            Placed: SdfPlacedFactory.FromPrimitive(new SdfPolygon(pts), _xf, mat),
            Style: style
        ));
        return this;
    }

    public IPathBuilder AddPath(in DrawStyle style)
        => new PathBuilderImplCollection(this, _xf, style);

    public IShapeCollectionBuilder ApplyShader(CodeDrawShader shader, Uniforms uniforms)
    {
        _shader = shader;
        _uniforms = uniforms;
        return this;
    }

    public void Draw()
    {
        if (_commands.Count == 0) return;

        // Pivot: applied at draw-time by shifting the whole collection around its bounds.
        // For now we do a simple broad approach: compute union bounds from known SDF bounds (world bounds).
        // (If you don’t want pivot now, delete this block.)
        if (_hasPivot)
        {
            Rect bb = default;
            var has = false;

            foreach (var c in _commands)
            {
                if (c is CmdSdf ds)
                {
                    var wbb = ds.Placed.WorldBounds;
                    bb = has ? bb.Union(wbb) : wbb;
                    has = true;
                }
            }

            if (has)
            {
                var px = MathG.Lerp(bb.Min.X, bb.Max.X, _pivotAx);
                var py = MathG.Lerp(bb.Min.Y, bb.Max.Y, _pivotAy);
                var t = Matrix3x3.CreateTranslation(-px, -py);

                // rewrite SdfPlaced transforms by pre-multiplying translation
                for (var i = 0; i < _commands.Count; i++)
                {
                    if (_commands[i] is CmdSdf ds)
                        _commands[i] = ds with { Placed = ds.Placed with { LocalToWorld = ds.Placed.LocalToWorld * t } };
                }
            }
        }

        _layer.Enqueue(new CmdBatch(_commands.ToArray()));

        // Shader stage is intentionally not implemented here because your engine already
        // has CustomDrawRect/PostProcess pipelines; wire it there when ready.
    }

    // A path builder that records into the collection instead of enqueuing immediately.
    private sealed class PathBuilderImplCollection(ShapeCollectionBuilderImpl col, in Matrix3x3 xf, in DrawStyle style) : IPathBuilder
    {
        private readonly ShapeCollectionBuilderImpl _col = col;
        private readonly PathBuilderImpl _inner = new(col._layer, xf, style);

        public IPathBuilder MoveTo(float x, float y) => _inner.MoveTo(x, y);
        public IPathBuilder LineTo(float x, float y) => _inner.LineTo(x, y);
        public IPathBuilder QuadTo(float cx, float cy, float x, float y) => _inner.QuadTo(cx, cy, x, y);
        public IPathBuilder CubicTo(float cx1, float cy1, float cx2, float cy2, float x, float y) => _inner.CubicTo(cx1, cy1, cx2, cy2, x, y);
        public IPathBuilder ArcTo(float cx, float cy, float radius, float startDeg, float sweepDeg, int segmentsHint = 0) => _inner.ArcTo(cx, cy, radius, startDeg, sweepDeg, segmentsHint);
        public IPathBuilder Close() => _inner.Close();
        public IPathBuilder Fill(ColorF fill) => _inner.Fill(fill);
        public IPathBuilder Stroke(in Stroke stroke) => _inner.Stroke(stroke);
        public IPathBuilder Paint(in Paint paint) => _inner.Paint(paint);
        public IPathBuilder Style(in DrawStyle style) => _inner.Style(style);

        public void Draw()
        {
            if (_inner.TryBuildCmd(out var cmd))
                _col._commands.Add(cmd);
        }
    }
}