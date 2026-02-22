using System.Runtime.CompilerServices;
using MarcoZechner.CodeDrawDotNet.Drawing;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.CodeDrawDotNet.Shaders;
using MarcoZechner.ColorDotNet.RGB;
using MarcoZechner.MathDotNet;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer;

public sealed partial class CodeDrawLayer : ICodeDrawShapes
{
    // ---------------------------
    // Transform stack
    // ---------------------------

    private readonly Stack<Matrix3x3> _xfStack = new();
    private Matrix3x3 _xf = Matrix3x3.Identity;

    public Matrix3x3 CurrentTransform => _xf;

    public void PushTransform(in Matrix3x3 m, TransformCombine combine = TransformCombine.MultiplyCurrent)
    {
        _xfStack.Push(_xf);
        _xf = combine == TransformCombine.Replace ? m : (_xf * m);
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

    public void Rect(in Rect r, in DrawStyle style)
        => throw new NotImplementedException();//Enqueue(new CmdDrawSdf(new SdfPlaced(new SdfRect(r), _xf), style));

    public void RoundedRect(in Rect r, float radius, in DrawStyle style)
        => throw new NotImplementedException();//Enqueue(new CmdDrawSdf(new SdfPlaced(new SdfRoundedRect(r, radius), _xf), style));

    public void Circle(Vector2 center, float radius, in DrawStyle style)
        => throw new NotImplementedException();//Enqueue(new CmdDrawSdf(new SdfPlaced(new SdfCircle(center, radius), _xf), style));

    public void Ellipse(Vector2 center, Vector2 radius, in DrawStyle style)
        => throw new NotImplementedException();//Enqueue(new CmdDrawSdf(new SdfPlaced(new SdfEllipse(center, radius), _xf), style));

    public void Triangle(in Vector2 a, in Vector2 b, in Vector2 c, in DrawStyle style)
        => throw new NotImplementedException();//Enqueue(new CmdDrawSdf(new SdfPlaced(new SdfTriangle(a, b, c), _xf), style));

    public void Polygon(ReadOnlySpan<Vector2> points, in DrawStyle style)
        => throw new NotImplementedException();//Enqueue(new CmdDrawSdf(new SdfPlaced(new SdfPolygon(points), _xf), style));

    public void Polyline(
        ReadOnlySpan<Vector2> points,
        in Stroke stroke,
        bool closed = false,
        BlendMode blend = BlendMode.SOURCE_OVER_ALPHA,
        float opacity = 1f)
    {
        var paint = Paint.StrokeOnly(stroke);
        var style = new DrawStyle(paint, blend, opacity, FeatherPx: 1f);
        throw new NotImplementedException();
        // Enqueue(new CmdDrawSdf(new SdfPlaced(new SdfPolyline(points, closed), _xf), style, forceStrokeOnly: true));
    }

    public void Line(Vector2 p0, Vector2 p1, in Stroke stroke, BlendMode blend = BlendMode.SOURCE_OVER_ALPHA, float opacity = 1f)
        => Polyline([p0, p1], stroke, closed: false, blend, opacity);

    // ---------------------------
    // Path + Collections
    // ---------------------------

    public IPathBuilder Path(in DrawStyle style = default)
        => throw new NotImplementedException();//new PathBuilderImpl(this, _xf, style);

    public IShapeCollectionBuilder ShapeCollection(in Matrix3x3? initialTransform = null)
        => throw new NotImplementedException();//new ShapeCollectionBuilderImpl(this, initialTransform ?? _xf);

    // =====================================================
    // INTERNAL COMMANDS + EXECUTION
    // =====================================================

    private readonly record struct CmdDrawSdf(
        SdfPlaced Placed,
        DrawStyle Style,
        bool forceStrokeOnly = false
    ) : ICmd
    {
        public void Exec(GL gl, CodeDrawLayer self)
        {
            throw new NotImplementedException();
            // if (!Placed.TryGetWorldToLocal(out var w2L))
            //     return;
            //
            // // Bounds in WORLD == layer space (because you use _xf as LocalToWorld and you draw in layer coords)
            // // Clamp to pixel bounds.
            // var bb = Placed.WorldBounds;
            //
            // var x0 = Math.Clamp((int)MathF.Floor(bb.Min.X) - 2, 0, self.Width - 1);
            // var y0 = Math.Clamp((int)MathF.Floor(bb.Min.Y) - 2, 0, self.Height - 1);
            // var x1 = Math.Clamp((int)MathF.Ceiling(bb.Max.X) + 2, 0, self.Width - 1);
            // var y1 = Math.Clamp((int)MathF.Ceiling(bb.Max.Y) + 2, 0, self.Height - 1);
            //
            // if (x1 < x0 || y1 < y0) return;
            //
            // // Feather in px (layer space) — you can later make this scale with transform if you want.
            // var feather = Style.FeatherPx;
            //
            // // Opacity multiplies BOTH fill+stroke alpha.
            // var opacity = Math.Clamp(Style.Opacity, 0f, 1f);
            //
            // // Paint components
            // var fill = Style.Paint.Fill;
            // var stroke = Style.Paint.Stroke;
            //
            // // Stroke thickness: interpret Align in SDF-world terms.
            // // For now: simplest is centered stroke band with thickness = stroke.Thickness.
            // // If you want Inside/Outside, you can offset signedDistance before StrokeAlpha.
            // var halfT = Math.Max(0f, stroke.Thickness) * 0.5f;
            //
            // // Raster
            // for (var y = y0; y <= y1; y++)
            // {
            //     var row = y * self.Width;
            //     for (var x = x0; x <= x1; x++)
            //     {
            //         var pWorld = new Vector2(x + 0.5f, y + 0.5f);
            //         var pLocal = Matrix3x3.TransformAffine(w2L, pWorld);
            //
            //         var d = Placed.Shape.DistanceLocal(pLocal);
            //
            //         // ---- compute alphas ----
            //         float aFill = 0f;
            //         float aStroke = 0f;
            //
            //         // Fill
            //         if (!forceStrokeOnly && fill.A > 0f)
            //             aFill = SdfCoverage.FillAlpha(d, feather) * fill.A;
            //
            //         // Stroke
            //         if (stroke.Thickness > 0f && stroke.Color.A > 0f)
            //         {
            //             // Center band:
            //             var baseStroke = SdfCoverage.StrokeAlpha(d, halfT, feather);
            //
            //             // Align adjustment (cheap + good enough):
            //             // Inside: shift band inward (treat boundary as inside by +halfT)
            //             // Outside: shift band outward (treat boundary as outside by -halfT)
            //             if (stroke.Align == StrokeAlign.Inside)
            //                 baseStroke = SdfCoverage.StrokeAlpha(d + halfT, halfT, feather);
            //             else if (stroke.Align == StrokeAlign.Outside)
            //                 baseStroke = SdfCoverage.StrokeAlpha(d - halfT, halfT, feather);
            //
            //             aStroke = baseStroke * stroke.Color.A;
            //         }
            //
            //         // Apply global opacity
            //         aFill *= opacity;
            //         aStroke *= opacity;
            //
            //         if (aFill <= 0f && aStroke <= 0f) continue;
            //
            //         // ---- composite fill+stroke order ----
            //         var dst = ctx.PixelsRgba8[row + x];
            //
            //         if (Style.Paint.Order == PaintOrder.FillThenStroke)
            //         {
            //             if (aFill > 0f) dst = BlendSourceOver(dst, PackColor(fill, aFill));
            //             if (aStroke > 0f) dst = BlendSourceOver(dst, PackColor(stroke.Color, aStroke));
            //         }
            //         else
            //         {
            //             if (aStroke > 0f) dst = BlendSourceOver(dst, PackColor(stroke.Color, aStroke));
            //             if (aFill > 0f) dst = BlendSourceOver(dst, PackColor(fill, aFill));
            //         }
            //
            //         ctx.PixelsRgba8[row + x] = dst;
            //     }
            // }
        }
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
        uint R = (uint)(r * 255f + 0.5f);
        uint G = (uint)(g * 255f + 0.5f);
        uint B = (uint)(b * 255f + 0.5f);
        uint A = (uint)(a * 255f + 0.5f);

        return (R) | (G << 8) | (B << 16) | (A << 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint BlendSourceOver(uint dst, uint src)
    {
        // Straight alpha "source over".
        // NOTE: you have BlendMode in the API; for now we only implement SOURCE_OVER_ALPHA.
        // Expand if you want additive, multiply, etc.

        var sr = (src) & 0xFF;
        var sg = (src >> 8) & 0xFF;
        var sb = (src >> 16) & 0xFF;
        var sa = (src >> 24) & 0xFF;

        if (sa == 0) return dst;
        if (sa == 255) return src;

        var dr = (dst) & 0xFF;
        var dg = (dst >> 8) & 0xFF;
        var db = (dst >> 16) & 0xFF;
        var da = (dst >> 24) & 0xFF;

        // out = src + dst*(1-sa)
        var invA = 255 - sa;

        uint or = (uint)(sr + (dr * invA + 127) / 255);
        uint og = (uint)(sg + (dg * invA + 127) / 255);
        uint ob = (uint)(sb + (db * invA + 127) / 255);
        uint oa = (uint)(sa + (da * invA + 127) / 255);

        return (or) | (og << 8) | (ob << 16) | (oa << 24);
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