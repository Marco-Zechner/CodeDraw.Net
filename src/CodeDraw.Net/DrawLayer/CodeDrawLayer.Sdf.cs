using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MarcoZechner.CodeDrawDotNet.Drawing;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfGpu;
using MarcoZechner.MathDotNet;
using Silk.NET.OpenGL;

namespace MarcoZechner.CodeDrawDotNet.DrawLayer;

public sealed unsafe partial class CodeDrawLayer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateFirstPrimOp(ReadOnlySpan<GpuSdfPrim> prims)
    {
        if (prims.Length == 0) return;

        var op0 = prims[0].Op;
        if (op0 == OP_SUBTRACT || op0 == OP_SMOOTH_SUB)
            throw new InvalidOperationException(
                "BUG: First GPU SDF primitive uses SUBTRACT/SMOOTH_SUB. " +
                "The shader assumes prim[0] is the base (A) distance. " +
                "Ensure the flattener emits a non-subtract op for the first primitive.");
    }
    
    private const int SSBO_BINDING_PRIMS = 0;

    internal static void ExecSdf(GL gl, CodeDrawLayer self, SdfPlaced placed, DrawStyle style, bool forceStrokeOnly)
    {
        // ExecSdfCpu(gl, self, placed, style, forceStrokeOnly);
        self.ExecSdfGpu(gl, placed, style, forceStrokeOnly);
    }

    private void ExecSdfGpu(GL gl, in SdfPlaced placed, in DrawStyle style, bool forceStrokeOnly)
    {
        if (_progSdf == null!) return; // not initialized
        if (!placed.TryGetWorldToLocal(out var w2LPlaced)) return;

        // Conservative bounds in layer space (world==layer px)
        var bb = (Rect<int>)placed.WorldBounds;

        var feather = MathG.Max(0f, style.FeatherPx);
        var pad = feather;

        var stroke = style.Paint.Stroke;
        if (stroke.Thickness > 0f && stroke.Color.A > 0f)
            pad = MathG.Max(pad, 0.5f * stroke.Thickness + feather);

        bb = bb.Expand((int)pad + 1);

        if (bb.Right < bb.Left || bb.Bottom < bb.Top) return;

        // Clip to layer
        var left   = Math.Clamp(bb.Left,   0, _w - 1);
        var right  = Math.Clamp(bb.Right,  0, _w - 1);
        var top    = Math.Clamp(bb.Top,    0, _h - 1);
        var bottom = Math.Clamp(bb.Bottom, 0, _h - 1);

        var w = (right - left + 1);
        var h = (bottom - top + 1);
        if (w <= 0 || h <= 0) return;

        // Build primitive list for the placed shape
        var prims = new List<GpuSdfPrim>(32);
        FlattenToGpuPrims(placed.Shape, w2LPlaced, prims);
        ValidateFirstPrimOp(CollectionsMarshal.AsSpan(prims));

        if (prims.Count == 0) return;

        // Upload SSBO: [int primCount][GpuSdfPrim ...]
        var headerSize = 16; // std430: treat as 16-byte block to be safe
        var primSize = sizeof(GpuSdfPrim);
        var totalBytes = headerSize + primSize * prims.Count;

        gl.BindBuffer(GLEnum.ShaderStorageBuffer, _sdfSsbo);
        gl.BufferData(GLEnum.ShaderStorageBuffer, (nuint)totalBytes, null, GLEnum.StreamDraw);

        // write header (primCount) into first 4 bytes; rest zero
        Span<byte> hdr = stackalloc byte[16];
        Unsafe.WriteUnaligned(ref hdr[0], prims.Count);
        fixed (byte* pHdr = hdr)
            gl.BufferSubData(GLEnum.ShaderStorageBuffer, 0, (nuint)hdr.Length, pHdr);

        // write prim array after header
        var span = CollectionsMarshal.AsSpan(prims);
        fixed (GpuSdfPrim* pPrims = span)
            gl.BufferSubData(GLEnum.ShaderStorageBuffer, headerSize, (nuint)(primSize * prims.Count), pPrims);

        gl.BindBufferBase(GLEnum.ShaderStorageBuffer, SSBO_BINDING_PRIMS, _sdfSsbo);
        gl.BindBuffer(GLEnum.ShaderStorageBuffer, 0);

        // Bind shader
        gl.UseProgram(_progSdf);
        gl.BindVertexArray(_vao);

        // uPosSize: quad in LAYER px
        // IMPORTANT: our sdf.vert uses uXf, but here we draw already in layer/world px -> use identity
        Uniform4F(gl, _uSdfPosSize, left, top, w, h);
        Uniform2F(gl, _uSdfRes, _w, _h);
        UniformMat3(gl, _uSdfXf, Matrix3x3.Identity);

        var opacity = Math.Clamp(style.Opacity, 0f, 1f);

        var fill = style.Paint.Fill;
        var hasFill = (!forceStrokeOnly && fill.A > 0f) ? 1 : 0;
        var strokeColor = stroke.Color;
        var hasStroke = (stroke.Thickness > 0f && strokeColor.A > 0f) ? 1 : 0;

        // apply opacity multiplicatively in shader via alpha (cheap)
        var fillA = fill.A * opacity;
        var strokeA = strokeColor.A * opacity;

        Uniform4F(gl, _uSdfFillColor, fill.R, fill.G, fill.B, fillA);
        Uniform4F(gl, _uSdfStrokeColor, strokeColor.R, strokeColor.G, strokeColor.B, strokeA);
        gl.Uniform1(_uSdfStrokeThickness, stroke.Thickness);
        gl.Uniform1(_uSdfFeatherPx, feather);
        gl.Uniform1(_uSdfHasFill, hasFill);
        gl.Uniform1(_uSdfHasStroke, hasStroke);

        // Blend mode already set by ApplyBlendMode()
        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);

        gl.BindVertexArray(0);
        gl.UseProgram(0);
    }

    // ---- Flattening ----

    // You’ll need to map these constants to GLSL.
    private const int T_CIRCLE = 1;
    private const int T_RECT = 2;
    private const int T_ROUNDEDRECT = 3;
    private const int T_SEGMENT = 4;
    private const int T_TRIANGLE = 5;
    private const int T_ELLIPSE = 6;

    private const int OP_UNION = 1;
    private const int OP_INTERSECT = 2;
    private const int OP_SUBTRACT = 3;
    private const int OP_SMOOTH_UNION = 4;
    private const int OP_SMOOTH_INTER = 5;
    private const int OP_SMOOTH_SUB = 6;

    private static void FlattenToGpuPrims(ISdf2 sdf, in Matrix3x3 worldToLocalRoot, List<GpuSdfPrim> outPrims)
    {
        if (sizeof(GpuSdfPrim) != 128)
            throw new InvalidOperationException($"GpuSdfPrim size is {sizeof(GpuSdfPrim)}, but expected 128 bytes. Check struct layout and padding.");
        
        // For a first pass:
        // - Union/Intersect fold in-order
        // - Subtract folds ok for hard subtract
        // - Smooth subtract N is NOT canonical; only support single B or pre-unioned B
        Emit(sdf, worldToLocalRoot, outPrims, isFirst:true);
    }

    private static void Emit(ISdf2 sdf, in Matrix3x3 worldToLocal, List<GpuSdfPrim> outPrims, bool isFirst)
    {
        switch (sdf)
        {
            case SdfTransform t:
            {
                // t.DistanceLocal(parentP) uses parent->child inverse.
                // We already operate in WORLD space in shader via worldToLocal. We want:
                // pChild = (childWorldToLocal) * pWorld
                // childWorldToLocal = (parentToChild?) compose with current worldToLocal.
                if (!Matrix3x3.TryInvert(t.LocalToParent, out var parentToChild))
                    throw new InvalidOperationException("SdfTransform requires invertible matrix.");

                var w2C = parentToChild * worldToLocal;
                Emit(t.Child, w2C, outPrims, isFirst);
                return;
            }

            case SdfUnionN u:
            {
                for (var i = 0; i < u.Children.Length; i++)
                    EmitWithOp(u.Children[i], worldToLocal, outPrims, i==0 && isFirst, OP_UNION, 0f);
                return;
            }

            case SdfIntersectN it:
            {
                for (var i = 0; i < it.Children.Length; i++)
                    EmitWithOp(it.Children[i], worldToLocal, outPrims, i==0 && isFirst, OP_INTERSECT, 0f);
                return;
            }

            case SdfSmoothUnionN su:
            {
                var k = MathF.Max(0f, su.K);
                for (var i = 0; i < su.Children.Length; i++)
                    EmitWithOp(su.Children[i], worldToLocal, outPrims, i==0 && isFirst, k>0f ? OP_SMOOTH_UNION : OP_UNION, k);
                return;
            }

            case SdfSubtractN sub:
            {
                // A - (union Bs) hard subtract folds fine with repeated max(acc, -d)
                Emit(sub.A, worldToLocal, outPrims, isFirst);

                for (var i = 0; i < sub.Bs.Length; i++)
                    EmitWithOp(sub.Bs[i], worldToLocal, outPrims, isFirst:false, OP_SUBTRACT, 0f);

                return;
            }

            case SdfSmoothIntersectN si:
            {
                var k = MathF.Max(0f, si.K);
                var op = k > 0f ? OP_SMOOTH_INTER : OP_INTERSECT;

                for (var i = 0; i < si.Children.Length; i++)
                    EmitWithOp(si.Children[i], worldToLocal, outPrims, i == 0 && isFirst, op, k);

                return;
            }

            case SdfSmoothSubtractN ss:
            {
                // IMPORTANT:
                // Your shader now implements canonical subtract-N:
                //   acc = A combined with any non-sub ops
                //   then subtracts are collected into union(Bs) and applied ONCE at end.
                //
                // That means the most compatible CPU encoding is:
                //   emit A as the first group
                //   emit each B as OP_SUBTRACT or OP_SMOOTH_SUB (k = ss.K)
                //
                // Caveat: If A contains non-sub compositions, those still work (union/intersect/smooth union etc.)
                // because they are combined via combine(acc, d, op, k) in the shader.
                //
                // Another caveat: This shader’s subtract model is "global": it subtracts union(Bs) from the final acc.
                // That matches SdfSmoothSubtractN’s intended semantics when it's the top-level op, or when you accept
                // this as the meaning of subtraction in the flattened representation.

                Emit(ss.A, worldToLocal, outPrims, isFirst);

                var k = MathF.Max(0f, ss.K);
                var op = k > 0f ? OP_SMOOTH_SUB : OP_SUBTRACT;

                foreach (var t in ss.Bs) 
                    EmitWithOp(t, worldToLocal, outPrims, isFirst: false, op, k);

                return;
            }
            
            case SdfPolygon poly:
            {
                EmitConvexPolygonAsTriangles(poly, worldToLocal, outPrims, isFirst);
                return;
            }
            
            case SdfPolyline pl:
            {
                EmitPolylineAsSegments(pl, worldToLocal, outPrims, isFirst);
                return;
            }
        }

        // primitive leaf
        outPrims.Add(MakePrim(sdf, worldToLocal, opOverride: OP_UNION, k: 0f));
    }

    private static void EmitWithOp(
        ISdf2 child,
        in Matrix3x3 w2L,
        List<GpuSdfPrim> outPrims,
        bool isFirst,
        int op,
        float k)
    {
        var before = outPrims.Count;
        Emit(child, w2L, outPrims, isFirst);

        if (outPrims.Count <= before) return;

        // We want the FIRST emitted prim of this child to carry its op/k,
        // except when this child is the first overall primitive in the whole list.
        // In that special case, the shader initializes acc = d and ignores op.
        // So: for the true first primitive, force OP_UNION (or just keep whatever it has).
        if (before == 0)
            return;

        var p = outPrims[before];
        p.Op = op;
        p.K = k;
        outPrims[before] = p;
    }

    private static GpuSdfPrim MakePrim(ISdf2 sdf, in Matrix3x3 worldToLocal, int opOverride, float k)
    {
        var p = new GpuSdfPrim
        {
            Type = 0,
            Op = opOverride,
            K = k
        };

        p.SetWorldToLocalFromAffine3x3(worldToLocal);

        switch (sdf)
        {
            case SdfCircle c:
                p.Type = T_CIRCLE;
                p.P0x = c.Center.X; p.P0y = c.Center.Y; p.P0z = c.Radius; p.P0w = 0f;
                break;

            case SdfRect r:
                p.Type = T_RECT;
                p.P0x = r.R.Left; p.P0y = r.R.Top; p.P0z = r.R.Right; p.P0w = r.R.Bottom;
                break;

            case SdfRoundedRect rr:
                p.Type = T_ROUNDEDRECT;
                p.P0x = rr.R.Left; p.P0y = rr.R.Top; p.P0z = rr.R.Right; p.P0w = rr.R.Bottom;
                p.P1x = rr.Radius;
                break;

            case SdfSegment s:
                p.Type = T_SEGMENT;
                p.P0x = s.A.X; p.P0y = s.A.Y; p.P0z = s.B.X; p.P0w = s.B.Y;
                p.P1x = s.Radius;
                break;

            case SdfTriangle t:
                p.Type = T_TRIANGLE;
                p.P0x = t.A.X; p.P0y = t.A.Y; p.P0z = t.B.X; p.P0w = t.B.Y;
                p.P1x = t.C.X; p.P1y = t.C.Y;
                break;

            case SdfEllipse e:
                p.Type = T_ELLIPSE;
                p.P0x = e.Center.X; p.P0y = e.Center.Y;
                p.P1x = e.Radius.X; p.P1y = e.Radius.Y;
                break;

            default:
                // Unknown => empty
                p.Type = 0;
                break;
        }

        return p;
    }
    
    private static void EmitConvexPolygonAsTriangles(
        SdfPolygon poly,
        in Matrix3x3 worldToLocal,
        List<GpuSdfPrim> outPrims,
        bool isFirst)
    {
        // You need a way to access the points from SdfPolygon.
        // Right now _pts is private, so add an internal getter in SdfPolygon:
        //   internal ReadOnlySpan<Vector2> Points => _pts;
        var pts = poly.Points;

        if (pts.Length < 3)
            return;

        // First triangle can be "first overall" (shader does acc=d on i==0).
        // Subsequent triangles should be UNION (carry op).
        for (var i = 1; i + 1 < pts.Length; i++)
        {
            var tri = new SdfTriangle(pts[0], pts[i], pts[i + 1]);

            // Emit triangle primitive
            // For the first triangle: respect isFirst
            // For others: force UNION op on the first primitive emitted for that triangle.
            Emit(tri, worldToLocal, outPrims, isFirst && i == 1);

            // If it's not the first overall triangle, enforce UNION on the first emitted prim.
            // (In practice Emit(tri, ...) will add exactly one prim, but keep it robust.)
            if (isFirst && i == 1) continue;

            var idx = outPrims.Count - 1;
            var p = outPrims[idx];
            p.Op = OP_UNION;
            p.K = 0f;
            outPrims[idx] = p;
        }
    }
    
    private static void EmitPolylineAsSegments(
        SdfPolyline pl,
        in Matrix3x3 worldToLocal,
        List<GpuSdfPrim> outPrims,
        bool isFirst)
    {
        var pts = pl.Points;
        if (pts.Length < 2) return;

        var radius = pl.Radius;

        // segments i-1 -> i
        var firstEmitted = false;
        for (var i = 1; i < pts.Length; i++)
        {
            EmitOneSegment(pts[i - 1], pts[i], radius, worldToLocal, outPrims, isFirst && !firstEmitted);
            firstEmitted = true;
        }

        if (pl.Closed)
        {
            EmitOneSegment(pts[^1], pts[0], radius, worldToLocal, outPrims, isFirst && !firstEmitted);
        }
    }
    
    private static void EmitOneSegment(
        Vector2 a,
        Vector2 b,
        float radius,
        in Matrix3x3 worldToLocal,
        List<GpuSdfPrim> outPrims,
        bool isFirstForScene)
    {
        var seg = new SdfSegment { A = a, B = b, Radius = radius };

        // Emit will add one primitive.
        Emit(seg, worldToLocal, outPrims, isFirstForScene);

        // If this isn't the very first primitive overall, union it in.
        if (isFirstForScene) return;

        var idx = outPrims.Count - 1;
        var p = outPrims[idx];
        p.Op = OP_UNION;
        p.K = 0f;
        outPrims[idx] = p;
    }
}