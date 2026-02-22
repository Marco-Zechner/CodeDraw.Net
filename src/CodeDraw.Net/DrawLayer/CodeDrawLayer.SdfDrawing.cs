using System.Runtime.CompilerServices;
using MarcoZechner.CodeDrawDotNet.Drawing;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.CodeDrawDotNet.DrawLayer.Commands;
using MarcoZechner.ColorDotNet.RGB;
using MarcoZechner.MathDotNet;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer;

public sealed partial class CodeDrawLayer : ICodeDrawShapes
{
    public readonly record struct SdfDrawToken(SdfPlaced Placed, DrawStyle Style)
    {
        /// <summary>
        /// Bounds in LAYER PIXEL SPACE (world==layer), padded to include AA feather and stroke.
        /// Use this for CPU raster loop bounds and debug rectangles.
        /// </summary>
        public Rect CoverageBoundsPx()
        {
            // Base world bounds of the primitive (no feather/stroke yet)
            var bb = Placed.WorldBounds;

            // Feather is always in px
            var feather = MathG.Max(0f, Style.FeatherPx);

            var pad = feather;

            var stroke = Style.Paint.Stroke;
            if (stroke is not { Thickness: > 0f, Color.A: > 0f }) return bb.Expand(pad);

            var halfT = 0.5f * MathG.Max(0f, stroke.Thickness);

            // StrokeAlign affects which side of the SDF boundary is covered.
            // For bounds, we need a safe pad:
            // - Inside: can still expand by feather (but not by halfT outside), however using halfT is safe.
            // - Outside: definitely expands outward by halfT.
            // - Center: expands by halfT both directions.
            //
            // We pick safe: include halfT always.
            pad = MathG.Max(pad, halfT + feather);

            // If fill-only and ForceStrokeOnly == true, only feather matters.
            // If ForceStrokeOnly == false and fill has alpha, feather already included.

            return bb.Expand(pad);
        }

        public void DrawDebugRect(CodeDrawLayer layer, ColorF color)
        {
            // IMPORTANT: draw debug rect in layer space (no current transform applied).
            // If your debug rect primitive honors transforms, temporarily reset.
            using (layer.PushTransformScope(Matrix3x3.Identity, TransformCombine.Replace))
            {
                var r = CoverageBoundsPx();
                layer.DrawDebugRect(r.Left, r.Top, r.Width, r.Height, color.R, color.G, color.B, color.A);
            }
        }
    }
    
    // ---------------------------
    // Transform stack
    // ---------------------------

    private readonly Stack<Matrix3x3> _xfStack = new();
    private Matrix3x3 _xf = Matrix3x3.Identity;

    public Matrix3x3 CurrentTransform => _xf;

    public void PushTransform(in Matrix3x3 m, TransformCombine combine = TransformCombine.MultiplyCurrent)
    {
        _xfStack.Push(_xf);
        _xf = combine == TransformCombine.Replace ? m : _xf * m;
    }

    public void PopTransform()
    {
        if (_xfStack.Count == 0)
            throw new InvalidOperationException("PopTransform() underflow.");
        _xf = _xfStack.Pop();
    }

    // ---------------------------
    // Shapes => enqueue SDF draw commands
    // ---------------------------

    public SdfDrawToken Rect(in Rect r, in DrawStyle style)
    {
        var t = new SdfDrawToken(new SdfPlaced(new SdfRect(r), _xf), style);
        Enqueue(new CmdSdf(t.Placed, t.Style));
        return t;
    }

    public SdfDrawToken RoundedRect(in Rect r, float radius, in DrawStyle style)
    {
        var t = new SdfDrawToken(new SdfPlaced(new SdfRoundedRect(r, radius), _xf), style);
        Enqueue(new CmdSdf(t.Placed, t.Style));
        return t;
    }

    public SdfDrawToken Circle(Vector2 center, float radius, in DrawStyle style)
    {
        var t = new SdfDrawToken(new SdfPlaced(new SdfCircle(center, radius), _xf), style);
        Enqueue(new CmdSdf(t.Placed, t.Style));
        return t;
    }

    public SdfDrawToken Ellipse(Vector2 center, Vector2 radius, in DrawStyle style)
    {
        var t = new SdfDrawToken(new SdfPlaced(new SdfEllipse(center, radius), _xf), style);
        Enqueue(new CmdSdf(t.Placed, t.Style));
        return t;
    }

    public SdfDrawToken Triangle(in Vector2 a, in Vector2 b, in Vector2 c, in DrawStyle style)
    {
        var t = new SdfDrawToken(new SdfPlaced(new SdfTriangle(a, b, c), _xf), style);
        Enqueue(new CmdSdf(t.Placed, t.Style));
        return t;
    }

    public SdfDrawToken Polygon(ReadOnlySpan<Vector2> points, in DrawStyle style)
    {
        var t = new SdfDrawToken(new SdfPlaced(new SdfPolygon(points), _xf), style);
        Enqueue(new CmdSdf(t.Placed, t.Style));
        return t;
    }

    public SdfDrawToken Polyline(ReadOnlySpan<Vector2> points, in Stroke stroke, bool closed = false,
        BlendMode blend = BlendMode.SOURCE_OVER_ALPHA, float opacity = 1f)
    {
        var style = new DrawStyle(Paint.StrokeOnly(stroke), blend, opacity, FeatherPx: 1f);
        var t = new SdfDrawToken(new SdfPlaced(new SdfPolyline(points, closed), _xf), style);
        Enqueue(new CmdSdf(t.Placed, t.Style));
        return t;
    }

    public SdfDrawToken Line(Vector2 p0, Vector2 p1, in Stroke stroke, BlendMode blend = BlendMode.SOURCE_OVER_ALPHA, float opacity = 1f)
    {
        var style = new DrawStyle(Paint.StrokeOnly(stroke), blend, opacity, FeatherPx: 1f);
        var t = new SdfDrawToken(new SdfPlaced(new SdfSegment(p0, p1), _xf), style);
        Enqueue(new CmdSdf(t.Placed, t.Style));
        return t;
    }
    
    // ---------------------------
    // Path + Collections
    // ---------------------------

    public IPathBuilder Path(in DrawStyle style = default)
        => new PathBuilderImpl(this, _xf, style);

    public IShapeCollectionBuilder ShapeCollection(in Matrix3x3? initialTransform = null) 
        => new ShapeCollectionBuilderImpl(this,  initialTransform ?? _xf);

    // =====================================================
    // INTERNAL COMMANDS + EXECUTION
    // =====================================================
    
    internal static void ExecSdf(GL gl, CodeDrawLayer self, SdfPlaced placed, DrawStyle style, bool forceStrokeOnly)
    {
        // Ensure CPU buffer exists + has base content for this frame
        self.ExecCpuBegin(gl, clear: false);

        var px = self._cpuRgba8;
        if (px == null) return;

        if (!placed.TryGetWorldToLocal(out var w2L))
        {
            Console.WriteLine("W2L failed");
            return;
        }

        // Conservative bounds in layer space (world == layer)
        Rect<int> bb = (Rect<int>)placed.WorldBounds;

        // same pad math as CoverageBoundsPx()
        var feather = MathG.Max(0f, style.FeatherPx);
        var pad = feather;
        var stroke = style.Paint.Stroke;
        if (stroke.Thickness > 0f && stroke.Color.A > 0f)
            pad = MathG.Max(pad, 0.5f * stroke.Thickness + feather);

        bb = bb.Expand((int)pad + 1); // +1 for rounding safety

        if (bb.Right < bb.Left || bb.Bottom < bb.Top) return;

        var opacity = Math.Clamp(style.Opacity, 0f, 1f);

        var fill = style.Paint.Fill;

        var halfT = Math.Max(0f, stroke.Thickness) * 0.5f;

        var left   = Math.Clamp(bb.Left,   0, self._w - 1);
        var right  = Math.Clamp(bb.Right,  0, self._w - 1);
        var top    = Math.Clamp(bb.Top,    0, self._h - 1);
        var bottom = Math.Clamp(bb.Bottom, 0, self._h - 1);
        
        for (var y = top; y <= bottom; y++)
        {
            var row = (self._h - 1 - y) * self._w;
            for (var x = left; x <= right; x++)
            {
                var pWorld = new Vector2(x + 0.5f, y + 0.5f);
                var pLocal = Matrix3x3.TransformAffine(w2L, pWorld);
                var d = placed.Shape.DistanceLocal(pLocal);

                float aFill = 0f;
                float aStroke = 0f;

                if (!forceStrokeOnly && fill.A > 0f)
                    aFill = SdfCoverage.FillAlpha(d, feather) * fill.A;

                if (stroke.Thickness > 0f && stroke.Color.A > 0f)
                {
                    float baseStroke;

                    if (stroke.Align == StrokeAlign.Inside)
                        baseStroke = SdfCoverage.StrokeAlpha(d + halfT, halfT, feather);
                    else if (stroke.Align == StrokeAlign.Outside)
                        baseStroke = SdfCoverage.StrokeAlpha(d - halfT, halfT, feather);
                    else
                        baseStroke = SdfCoverage.StrokeAlpha(d, halfT, feather);

                    aStroke = baseStroke * stroke.Color.A;
                }

                aFill *= opacity;
                aStroke *= opacity;

                if (aFill <= 0f && aStroke <= 0f) continue;

                var dst = px[row + x];

                // BlendMode handling: CPU debug supports a subset.
                // For now: SOURCE_OVER_ALPHA + NONE (overwrite).
                if (style.Blend == BlendMode.NONE)
                {
                    if (style.Paint.Order == PaintOrder.FillThenStroke)
                    {
                        if (aFill > 0f) dst = PackColor(fill, aFill);
                        if (aStroke > 0f) dst = PackColor(stroke.Color, aStroke);
                    }
                    else
                    {
                        if (aStroke > 0f) dst = PackColor(stroke.Color, aStroke);
                        if (aFill > 0f) dst = PackColor(fill, aFill);
                    }
                }
                else
                {
                    if (style.Paint.Order == PaintOrder.FillThenStroke)
                    {
                        if (aFill > 0f) dst = BlendSourceOver(dst, PackColor(fill, aFill));
                        if (aStroke > 0f) dst = BlendSourceOver(dst, PackColor(stroke.Color, aStroke));
                    }
                    else
                    {
                        if (aStroke > 0f) dst = BlendSourceOver(dst, PackColor(stroke.Color, aStroke));
                        if (aFill > 0f) dst = BlendSourceOver(dst, PackColor(fill, aFill));
                    }
                }

                px[row + x] = dst;
            }
        }

        self._cpuDirty = true;
    }

    // =====================================================
    // Color packing + blending (RGBA8)
    // =====================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint PackColor(ColorF c, float alphaMul)
    {
        var a = Math.Clamp(c.A * alphaMul, 0f, 1f);
        var r = Math.Clamp(c.R, 0f, 1f);
        var g = Math.Clamp(c.G, 0f, 1f);
        var b = Math.Clamp(c.B, 0f, 1f);

        // straight alpha RGBA8
        var R = (uint)(r * 255f + 0.5f);
        var G = (uint)(g * 255f + 0.5f);
        var B = (uint)(b * 255f + 0.5f);
        var A = (uint)(a * 255f + 0.5f);

        return R | G << 8 | B << 16 | A << 24;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint BlendSourceOver(uint dst, uint src)
    {
        // Straight alpha "source over".
        // NOTE: you have BlendMode in the API; for now we only implement SOURCE_OVER_ALPHA.
        // Expand if you want additive, multiply, etc.

        var sr = src & 0xFF;
        var sg = src >> 8 & 0xFF;
        var sb = src >> 16 & 0xFF;
        var sa = src >> 24 & 0xFF;

        switch (sa)
        {
            case 0: return dst;
            case 255: return src;
        }

        var dr = dst & 0xFF;
        var dg = dst >> 8 & 0xFF;
        var db = dst >> 16 & 0xFF;
        var da = dst >> 24 & 0xFF;

        // out = src + dst*(1-sa)
        var invA = 255 - sa;

        var or = sr + (dr * invA + 127) / 255;
        var og = sg + (dg * invA + 127) / 255;
        var ob = sb + (db * invA + 127) / 255;
        var oa = sa + (da * invA + 127) / 255;

        return or | og << 8 | ob << 16 | oa << 24;
    }

    // =====================================================
    // Renderer hook (YOU wire this into your existing drain loop)
    // =====================================================
    // Somewhere in your render thread when you have a writable pixel buffer:
    //
    // var ctx = new CmdExecContext(Width, Height, pixelSpan);
    // while (TryDequeue(out var cmd)) cmd.Execute(ctx);
    //
    // Then upload/publish like you already do.
}