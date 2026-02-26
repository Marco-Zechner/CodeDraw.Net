// CodeDrawLayer.SdfGpu.cs  (UPDATED: upload materials + rules + per-prim MatId tagging)
//
// You need 2 extra SSBOs in CodeDrawLayer:
//   private uint _sdfMatSsbo;
//   private uint _sdfRuleSsbo;
//
// And in EnsureInit():
//   _sdfMatSsbo  = _gl.GenBuffer();
//   _sdfRuleSsbo = _gl.GenBuffer();
//
// Also: bind points in shader are fixed: prims=0, mats=1, rules=2.

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
    private const int SSBO_BINDING_PRIMS  = 0;
    private const int SSBO_BINDING_MATS   = 1;
    private const int SSBO_BINDING_RULES  = 2;

    private readonly SdfGpuMaterialPacker _sdfPacker = new();

    private static Rect<int> Union(Rect<int> a, Rect<int> b)
    {
        var left   = Math.Min(a.Left, b.Left);
        var top    = Math.Min(a.Top, b.Top);
        var right  = Math.Max(a.Right, b.Right);
        var bottom = Math.Max(a.Bottom, b.Bottom);
        return new Rect<int>((left, top, right, bottom));
    }
    
    //TODO: forceStrokeOnly is currently a no-op
    private void ExecSdfGpu(GL gl, in SdfPlaced placed, in DrawStyle style, bool forceStrokeOnly, SdfDrawAreaOverride? drawAreaOverride, int maxBlendSdfs = 8)
    {
        if (_progSdf == null!) return;
        if (!placed.TryGetWorldToLocal(out var w2LPlaced)) return;

        // 1) Normal conservative bounds in layer space (world == layer px)
        var bbTight = (Rect<int>)placed.WorldBounds;

        // Compute pad like you already do (based on style).
        var feather = MathG.Max(0f, style.FeatherPx);
        var pad = feather;

        var stroke = style.Paint.Stroke;
        if (stroke.Thickness > 0f && stroke.Color.A > 0f)
            pad = MathG.Max(pad, 0.5f * stroke.Thickness + feather);

        bbTight = bbTight.Expand((int)pad + 2);

        // 2) Apply draw area override
        var bb = bbTight;

        if (drawAreaOverride is { } ov)
        {
            bb = ov.Mode == SdfDrawAreaMode.Replace 
                ? ov.RectPx 
                : Union(bbTight, ov.RectPx);
        }

        // 3) Clamp to layer bounds (same as you already do)
        if (bb.Right < bb.Left || bb.Bottom < bb.Top) return;

        var left   = Math.Clamp(bb.Left,   0, _w - 1);
        var right  = Math.Clamp(bb.Right,  0, _w - 1);
        var top    = Math.Clamp(bb.Top,    0, _h - 1);
        var bottom = Math.Clamp(bb.Bottom, 0, _h - 1);

        var w = right - left + 1;
        var h = bottom - top + 1;
        if (w <= 0 || h <= 0) return;

        // Build prim list + pack materials/rules
        _sdfPacker.Clear();

        var prims = new List<GpuSdfPrim>(64);
        FlattenToGpuPrims(placed.Shape, w2LPlaced, prims, _sdfPacker, currentMatId: 0, isFirst:true);

        if (prims.Count == 0) return;

        UploadPrims(gl, prims);
        UploadMaterials(gl, _sdfPacker.Materials);
        UploadRules(gl, _sdfPacker.Rules);

        // Bind shader
        gl.UseProgram(_progSdf);
        gl.BindVertexArray(_vao);

        GlHelper.Uniform4F(gl, _uSdfPosSize, left, top, w, h);
        GlHelper.Uniform2F(gl, _uSdfRes, _w, _h);
        GlHelper.UniformMat3(gl, _uSdfXf, Matrix3x3.Identity);

        GlHelper.Uniform1(gl, _uMaxBlendSdfs, maxBlendSdfs);

        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null);

        gl.BindVertexArray(0);
        gl.UseProgram(0);
    }

    private void UploadPrims(GL gl, List<GpuSdfPrim> prims)
    {
        var headerSize = 16;
        var primSize = sizeof(GpuSdfPrim);
        var totalBytes = headerSize + primSize * prims.Count;

        gl.BindBuffer(GLEnum.ShaderStorageBuffer, _sdfSsbo);
        gl.BufferData(GLEnum.ShaderStorageBuffer, (nuint)totalBytes, null, GLEnum.StreamDraw);

        Span<byte> hdr = stackalloc byte[16];
        Unsafe.WriteUnaligned(ref hdr[0], prims.Count);
        fixed (byte* pHdr = hdr)
            gl.BufferSubData(GLEnum.ShaderStorageBuffer, 0, (nuint)hdr.Length, pHdr);

        var span = CollectionsMarshal.AsSpan(prims);
        fixed (GpuSdfPrim* pPrims = span)
            gl.BufferSubData(GLEnum.ShaderStorageBuffer, headerSize, (nuint)(primSize * prims.Count), pPrims);

        gl.BindBufferBase(GLEnum.ShaderStorageBuffer, SSBO_BINDING_PRIMS, _sdfSsbo);
        gl.BindBuffer(GLEnum.ShaderStorageBuffer, 0);
    }

    private void UploadMaterials(GL gl, ReadOnlySpan<GpuSdfMaterial> mats)
    {
        // SSBO header: [int materialCount][padding]
        var headerSize = 16;
        var matSize = sizeof(GpuSdfMaterial);
        var totalBytes = headerSize + matSize * mats.Length;

        gl.BindBuffer(GLEnum.ShaderStorageBuffer, _sdfMatSsbo);
        gl.BufferData(GLEnum.ShaderStorageBuffer, (nuint)totalBytes, null, GLEnum.StreamDraw);

        Span<byte> hdr = stackalloc byte[16];
        Unsafe.WriteUnaligned(ref hdr[0], mats.Length);
        fixed (byte* pHdr = hdr)
            gl.BufferSubData(GLEnum.ShaderStorageBuffer, 0, (nuint)hdr.Length, pHdr);

        fixed (GpuSdfMaterial* p = mats)
            gl.BufferSubData(GLEnum.ShaderStorageBuffer, headerSize, (nuint)(matSize * mats.Length), p);

        gl.BindBufferBase(GLEnum.ShaderStorageBuffer, SSBO_BINDING_MATS, _sdfMatSsbo);
        gl.BindBuffer(GLEnum.ShaderStorageBuffer, 0);
    }

    private void UploadRules(GL gl, ReadOnlySpan<GpuSdfColorRule> rules)
    {
        var headerSize = 16;
        var ruleSize = sizeof(GpuSdfColorRule);
        var totalBytes = headerSize + ruleSize * rules.Length;

        gl.BindBuffer(GLEnum.ShaderStorageBuffer, _sdfRuleSsbo);
        gl.BufferData(GLEnum.ShaderStorageBuffer, (nuint)totalBytes, null, GLEnum.StreamDraw);

        Span<byte> hdr = stackalloc byte[16];
        Unsafe.WriteUnaligned(ref hdr[0], rules.Length);
        fixed (byte* pHdr = hdr)
            gl.BufferSubData(GLEnum.ShaderStorageBuffer, 0, (nuint)hdr.Length, pHdr);

        fixed (GpuSdfColorRule* p = rules)
            gl.BufferSubData(GLEnum.ShaderStorageBuffer, headerSize, (nuint)(ruleSize * rules.Length), p);

        gl.BindBufferBase(GLEnum.ShaderStorageBuffer, SSBO_BINDING_RULES, _sdfRuleSsbo);
        gl.BindBuffer(GLEnum.ShaderStorageBuffer, 0);
    }

    // -------------------------------------------------------------------------
    // Flattening (UPDATED): carries a currentMatId and supports SdfMaterialTag
    // -------------------------------------------------------------------------

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

    private static void FlattenToGpuPrims(
        ISdf2 sdf,
        in Matrix3x3 worldToLocal,
        List<GpuSdfPrim> outPrims,
        SdfGpuMaterialPacker packer,
        int currentMatId,
        bool isFirst)
    {
        // Keep your size check
        if (sizeof(GpuSdfPrim) != 128)
            throw new InvalidOperationException($"GpuSdfPrim size is {sizeof(GpuSdfPrim)}, expected 128.");

        Emit(sdf, worldToLocal, outPrims, packer, currentMatId, isFirst);
    }

    private static void Emit(
        ISdf2 sdf,
        in Matrix3x3 worldToLocal,
        List<GpuSdfPrim> outPrims,
        SdfGpuMaterialPacker packer,
        int currentMatId,
        bool isFirst)
    {
        switch (sdf)
        {
            case SdfMaterialTag tag:
            {
                var matId = packer.GetOrAdd(tag.Material);
                Emit(tag.Child, worldToLocal, outPrims, packer, matId, isFirst);
                return;
            }

            case SdfTransform t:
            {
                if (!Matrix3x3.TryInvert(t.LocalToParent, out var parentToChild))
                    throw new InvalidOperationException("SdfTransform requires invertible matrix.");
                var w2C = parentToChild * worldToLocal;
                Emit(t.Child, w2C, outPrims, packer, currentMatId, isFirst);
                return;
            }

            case SdfUnionN u:
            {
                for (var i = 0; i < u.Children.Length; i++)
                    EmitWithOp(u.Children[i], worldToLocal, outPrims, packer, currentMatId, i==0 && isFirst, OP_UNION, 0f);
                return;
            }

            case SdfIntersectN it:
            {
                for (var i = 0; i < it.Children.Length; i++)
                    EmitWithOp(it.Children[i], worldToLocal, outPrims, packer, currentMatId, i==0 && isFirst, OP_INTERSECT, 0f);
                return;
            }

            case SdfSmoothUnionN su:
            {
                var k = MathF.Max(0f, su.K);
                var op = k > 0f ? OP_SMOOTH_UNION : OP_UNION;
                for (var i = 0; i < su.Children.Length; i++)
                    EmitWithOp(su.Children[i], worldToLocal, outPrims, packer, currentMatId, i==0 && isFirst, op, k);
                return;
            }

            case SdfSmoothIntersectN si:
            {
                var k = MathF.Max(0f, si.K);
                var op = k > 0f ? OP_SMOOTH_INTER : OP_INTERSECT;
                for (var i = 0; i < si.Children.Length; i++)
                    EmitWithOp(si.Children[i], worldToLocal, outPrims, packer, currentMatId, i==0 && isFirst, op, k);
                return;
            }

            case SdfSubtractN sub:
            {
                Emit(sub.A, worldToLocal, outPrims, packer, currentMatId, isFirst);
                for (var i = 0; i < sub.Bs.Length; i++)
                    EmitWithOp(sub.Bs[i], worldToLocal, outPrims, packer, currentMatId, isFirst:false, OP_SUBTRACT, 0f);
                return;
            }

            case SdfSmoothSubtractN ss:
            {
                Emit(ss.A, worldToLocal, outPrims, packer, currentMatId, isFirst);

                var k = MathF.Max(0f, ss.K);
                var op = k > 0f ? OP_SMOOTH_SUB : OP_SUBTRACT;
                foreach (var b in ss.Bs)
                    EmitWithOp(b, worldToLocal, outPrims, packer, currentMatId, isFirst:false, op, k);
                return;
            }

            case SdfPolygon poly:
            {
                EmitConvexPolygonAsTriangles(poly, worldToLocal, outPrims, packer, currentMatId, isFirst);
                return;
            }

            case SdfPolyline pl:
            {
                EmitPolylineAsSegments(pl, worldToLocal, outPrims, packer, currentMatId, isFirst);
                return;
            }
        }

        outPrims.Add(MakePrim(sdf, worldToLocal, currentMatId, opOverride: OP_UNION, k: 0f));
    }

    private static void EmitWithOp(
        ISdf2 child,
        in Matrix3x3 w2L,
        List<GpuSdfPrim> outPrims,
        SdfGpuMaterialPacker packer,
        int currentMatId,
        bool isFirst,
        int op,
        float k)
    {
        var before = outPrims.Count;
        Emit(child, w2L, outPrims, packer, currentMatId, isFirst);

        if (outPrims.Count <= before) return;
        if (before == 0) return; // first prim is base; shader ignores op

        var p = outPrims[before];
        p.Op = op;
        p.K = k;
        outPrims[before] = p;
    }

    private static GpuSdfPrim MakePrim(ISdf2 sdf, in Matrix3x3 worldToLocal, int matId, int opOverride, float k)
    {
        var p = new GpuSdfPrim
        {
            Type = 0,
            Op = opOverride,
            MatId = matId,
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
                p.Type = 0;
                break;
        }

        return p;
    }

    private static void EmitConvexPolygonAsTriangles(
        SdfPolygon poly,
        in Matrix3x3 worldToLocal,
        List<GpuSdfPrim> outPrims,
        SdfGpuMaterialPacker packer,
        int currentMatId,
        bool isFirst)
    {
        var pts = poly.Points;
        if (pts.Length < 3) return;

        for (var i = 1; i + 1 < pts.Length; i++)
        {
            var tri = new SdfTriangle(pts[0], pts[i], pts[i + 1]);

            // Emit one prim
            outPrims.Add(MakePrim(tri, worldToLocal, currentMatId, OP_UNION, 0f));

            // First overall prim is base; leave as-is
            if (isFirst && i == 1) continue;

            // Ensure union op for subsequent triangles
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
        SdfGpuMaterialPacker packer,
        int currentMatId,
        bool isFirst)
    {
        var pts = pl.Points;
        if (pts.Length < 2) return;

        var radius = pl.Radius;

        var firstEmitted = false;

        for (var i = 1; i < pts.Length; i++)
        {
            EmitOneSegment(pts[i - 1], pts[i], radius, worldToLocal, outPrims, currentMatId, isFirst && !firstEmitted);
            firstEmitted = true;
        }

        if (pl.Closed)
            EmitOneSegment(pts[^1], pts[0], radius, worldToLocal, outPrims, currentMatId, isFirst && !firstEmitted);
    }

    private static void EmitOneSegment(
        Vector2 a,
        Vector2 b,
        float radius,
        in Matrix3x3 worldToLocal,
        List<GpuSdfPrim> outPrims,
        int currentMatId,
        bool isFirstForScene)
    {
        var seg = new SdfSegment { A = a, B = b, Radius = radius };
        outPrims.Add(MakePrim(seg, worldToLocal, currentMatId, OP_UNION, 0f));

        if (!isFirstForScene)
        {
            var idx = outPrims.Count - 1;
            var p = outPrims[idx];
            p.Op = OP_UNION;
            p.K = 0f;
            outPrims[idx] = p;
        }
    }
}