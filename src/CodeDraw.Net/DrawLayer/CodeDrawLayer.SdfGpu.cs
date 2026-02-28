using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MarcoZechner.CodeDrawDotNet.Drawing;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.Sdf.Primitives;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfGpu;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Composition;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Primitives;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Transform;
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

    private void ExecSdfGpu(GL gl, in SdfPlaced placed, in DrawStyle style, bool forceStrokeOnly, SdfDrawAreaOverride? drawAreaOverride, int maxBlendSdfs = 8)
    {
        if (_progSdf == null!) return;
        if (!placed.TryGetWorldToLocal(out var w2LPlaced)) return;

        // 1) Normal conservative bounds in layer space (world == layer px)
        var bbTight = (Rect<int>)placed.WorldBounds;

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

        // 3) Clamp to layer bounds
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

        // NOTE: you MUST pass the root node here; the compiled ISdf2 does not carry Material.
        // So SdfPlaced needs a RootNode (or CmdSdf carries it).
        FlattenToGpuPrims(placed.RootNode, w2LPlaced, prims, _sdfPacker, forceStrokeOnly);

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
    // Flattening (Node-walk): resolve Material on SdfNodeBase, emit prims with MatId.
    // -------------------------------------------------------------------------

    private const int CIRCLE = 1;
    private const int RECT = 2;
    private const int ROUNDEDRECT = 3;
    private const int SEGMENT = 4;
    private const int TRIANGLE = 5;
    private const int ELLIPSE = 6;

    private const int OP_UNION = 1;
    private const int OP_INTERSECT = 2;
    private const int OP_SUBTRACT = 3;
    private const int OP_SMOOTH_UNION = 4;
    private const int OP_SMOOTH_INTER = 5;
    private const int OP_SMOOTH_SUB = 6;

    private static void FlattenToGpuPrims(
        ISdf2Node root,
        in Matrix3x3 worldToLocal,
        List<GpuSdfPrim> outPrims,
        SdfGpuMaterialPacker packer,
        bool forceStrokeOnly)
    {
        if (sizeof(GpuSdfPrim) != 128)
            throw new InvalidOperationException($"GpuSdfPrim size is {sizeof(GpuSdfPrim)}, expected 128.");

        var ctx = new SdfCompileContext();
        outPrims.Clear();
        packer.Clear();

        var defaultMat = new SdfActiveMaterial(SdfDefaultMaterial.Instance, SdfColorOverwrite.OnlyDefault);
        EmitNode(root, worldToLocal, outPrims, packer, ctx, defaultMat, true, forceStrokeOnly);
    }

    private static SdfActiveMaterial ResolveActive(in SdfActiveMaterial active, SdfMaterial? nodeMat)
    {
        if (nodeMat == null) return active;

        // Parent forces everything: ignore child materials.
        if (active.Overwrite == SdfColorOverwrite.Everything)
            return active;

        // OnlyDefault: child overrides parent (only when child has a material at all).
        return new SdfActiveMaterial(nodeMat, nodeMat.Overwrite);
    }

    private static void EmitNode(
        ISdf2Node node,
        in Matrix3x3 worldToLocal,
        List<GpuSdfPrim> outPrims,
        SdfGpuMaterialPacker packer,
        SdfCompileContext ctx,
        SdfActiveMaterial activeMat,
        bool isFirst,
        bool forceStrokeOnly)
    {
        if (node is SdfNodeBase nb)
            activeMat = ResolveActive(activeMat, nb.Material);

        switch (node)
        {
            // ----------------- Transform -----------------
            case SdfTransformNode t:
            {
                if (!Matrix3x3.TryInvert(t.LocalToParent, out var parentToChild))
                    throw new InvalidOperationException("SdfTransformNode requires invertible LocalToParent.");

                var w2C = parentToChild * worldToLocal;
                EmitNode(t.Child, w2C, outPrims, packer, ctx, activeMat, isFirst, forceStrokeOnly);
                return;
            }

            // ----------------- Composition -----------------
            case SdfUnionNode u:
            {
                var children = u.Children ?? [];
                for (int i = 0; i < children.Length; i++)
                    EmitNodeWithOp(children[i], worldToLocal, outPrims, packer, ctx, activeMat,
                        i == 0 && isFirst, OP_UNION, 0f, forceStrokeOnly);
                return;
            }

            case SdfIntersectNode it:
            {
                var children = it.Children ?? [];
                for (int i = 0; i < children.Length; i++)
                    EmitNodeWithOp(children[i], worldToLocal, outPrims, packer, ctx, activeMat,
                        i == 0 && isFirst, OP_INTERSECT, 0f, forceStrokeOnly);
                return;
            }

            case SdfSmoothUnionNode su:
            {
                var children = su.Children ?? [];
                var k = MathF.Max(0f, su.K);
                var op = k > 0f ? OP_SMOOTH_UNION : OP_UNION;

                for (int i = 0; i < children.Length; i++)
                    EmitNodeWithOp(children[i], worldToLocal, outPrims, packer, ctx, activeMat,
                        i == 0 && isFirst, op, k, forceStrokeOnly);
                return;
            }

            case SdfSmoothIntersectNode si:
            {
                var children = si.Children ?? [];
                var k = MathF.Max(0f, si.K);
                var op = k > 0f ? OP_SMOOTH_INTER : OP_INTERSECT;

                for (int i = 0; i < children.Length; i++)
                    EmitNodeWithOp(children[i], worldToLocal, outPrims, packer, ctx, activeMat,
                        i == 0 && isFirst, op, k, forceStrokeOnly);
                return;
            }

            case SdfSubtractNode sub:
            {
                EmitNode(sub.A, worldToLocal, outPrims, packer, ctx, activeMat, isFirst, forceStrokeOnly);

                var bs = sub.Bs ?? [];
                for (int i = 0; i < bs.Length; i++)
                    EmitNodeWithOp(bs[i], worldToLocal, outPrims, packer, ctx, activeMat,
                        false, OP_SUBTRACT, 0f, forceStrokeOnly);
                return;
            }

            case SdfSmoothSubtractNode ss:
            {
                EmitNode(ss.A, worldToLocal, outPrims, packer, ctx, activeMat, isFirst, forceStrokeOnly);

                var bs = ss.Bs ?? [];
                var k = MathF.Max(0f, ss.K);
                var op = k > 0f ? OP_SMOOTH_SUB : OP_SUBTRACT;

                for (int i = 0; i < bs.Length; i++)
                    EmitNodeWithOp(bs[i], worldToLocal, outPrims, packer, ctx, activeMat,
                        false, op, k, forceStrokeOnly);
                return;
            }

            // ----------------- Primitives (node types) -----------------
            case SdfCircleNode:
            case SdfRectNode:
            case SdfRoundedRectNode:
            case SdfTriangleNode:
            case SdfEllipseNode:
            case SdfSegmentNode:
            {
                var sdf = SdfCompiler.Compile(node, ctx);
                var matId = packer.GetOrAdd(activeMat.Material, forceStrokeOnly);
                outPrims.Add(MakePrim(sdf, worldToLocal, matId, OP_UNION, 0f));
                return;
            }

            // Polygon/polyline nodes: emit multiple prims while preserving material/op behavior.
            case SdfPolygonNode polyNode:
            {
                var sdf = SdfCompiler.Compile(polyNode, ctx);
                var matId = packer.GetOrAdd(activeMat.Material, forceStrokeOnly);

                if (sdf is SdfPolygon poly)
                {
                    EmitConvexPolygonAsTriangles(poly, worldToLocal, outPrims, matId, isFirst);
                    return;
                }

                // Fallback (shouldn't happen): treat as single prim if possible
                outPrims.Add(MakePrim(sdf, worldToLocal, matId, OP_UNION, 0f));
                return;
            }

            case SdfPolylineNode plNode:
            {
                var sdf = SdfCompiler.Compile(plNode, ctx);
                var matId = packer.GetOrAdd(activeMat.Material, forceStrokeOnly);

                if (sdf is SdfPolyline pl)
                {
                    EmitPolylineAsSegments(pl, worldToLocal, outPrims, matId, isFirst);
                    return;
                }

                outPrims.Add(MakePrim(sdf, worldToLocal, matId, OP_UNION, 0f));
                return;
            }
            
            case SdfMaterialOverrideNode mo:
            {
                EmitNode(mo.Child, worldToLocal, outPrims, packer, ctx, activeMat, isFirst, forceStrokeOnly);
                return;
            }

            // ----------------- Unknown node fallback -----------------
            default:
            {
                // WARNING: This collapses the subtree and therefore loses child-material detail.
                // It is here only as a "don't crash" fallback.
                var sdf = SdfCompiler.Compile(node, ctx);
                var matId = packer.GetOrAdd(activeMat.Material, forceStrokeOnly);
                outPrims.Add(MakePrim(sdf, worldToLocal, matId, OP_UNION, 0f));
                return;
            }
        }
    }

    private static void EmitNodeWithOp(
        ISdf2Node child,
        in Matrix3x3 w2L,
        List<GpuSdfPrim> outPrims,
        SdfGpuMaterialPacker packer,
        SdfCompileContext ctx,
        SdfActiveMaterial activeMat,
        bool isFirst,
        int op,
        float k,
        bool forceStrokeOnly)
    {
        var before = outPrims.Count;
        EmitNode(child, w2L, outPrims, packer, ctx, activeMat, isFirst, forceStrokeOnly);

        if (outPrims.Count <= before) return;
        if (before == 0) return;

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
                p.Type = CIRCLE;
                p.P0x = c.Center.X; p.P0y = c.Center.Y; p.P0z = c.Radius; p.P0w = 0f;
                break;

            case SdfRect r:
                p.Type = RECT;
                p.P0x = r.R.Left; p.P0y = r.R.Top; p.P0z = r.R.Right; p.P0w = r.R.Bottom;
                break;

            case SdfRoundedRect rr:
                p.Type = ROUNDEDRECT;
                p.P0x = rr.R.Left; p.P0y = rr.R.Top; p.P0z = rr.R.Right; p.P0w = rr.R.Bottom;
                p.P1x = rr.Radius;
                break;

            case SdfSegment s:
                p.Type = SEGMENT;
                p.P0x = s.A.X; p.P0y = s.A.Y; p.P0z = s.B.X; p.P0w = s.B.Y;
                p.P1x = s.Radius;
                break;

            case SdfTriangle t:
                p.Type = TRIANGLE;
                p.P0x = t.A.X; p.P0y = t.A.Y; p.P0z = t.B.X; p.P0w = t.B.Y;
                p.P1x = t.C.X; p.P1y = t.C.Y;
                break;

            case SdfEllipse e:
                p.Type = ELLIPSE;
                p.P0x = e.Center.X; p.P0y = e.Center.Y;
                p.P1x = e.Radius.X; p.P1y = e.Radius.Y;
                break;

            default:
                p.Type = 0;
                break;
        }

        return p;
    }

    // NOTE: This assumes polygon is convex (your earlier helper name said "Convex").
    private static void EmitConvexPolygonAsTriangles(
        SdfPolygon poly,
        in Matrix3x3 worldToLocal,
        List<GpuSdfPrim> outPrims,
        int matId,
        bool isFirstForScene)
    {
        var pts = poly.Points;
        if (pts.Length < 3) return;

        var firstEmitted = false;

        for (var i = 1; i + 1 < pts.Length; i++)
        {
            var tri = new SdfTriangle(pts[0], pts[i], pts[i + 1]);

            outPrims.Add(MakePrim(tri, worldToLocal, matId, OP_UNION, 0f));

            // If this is the FIRST prim of the whole scene, it should remain base (shader ignores op anyway).
            if (isFirstForScene && !firstEmitted)
            {
                firstEmitted = true;
                continue;
            }

            // Ensure union op for subsequent triangles
            var idx = outPrims.Count - 1;
            var p = outPrims[idx];
            p.Op = OP_UNION;
            p.K = 0f;
            outPrims[idx] = p;

            firstEmitted = true;
        }
    }

    private static void EmitPolylineAsSegments(
        SdfPolyline pl,
        in Matrix3x3 worldToLocal,
        List<GpuSdfPrim> outPrims,
        int matId,
        bool isFirstForScene)
    {
        var pts = pl.Points;
        if (pts.Length < 2) return;

        var radius = pl.Radius;

        var firstEmitted = false;

        for (var i = 1; i < pts.Length; i++)
        {
            EmitOneSegment(pts[i - 1], pts[i], radius, worldToLocal, outPrims, matId, isFirstForScene && !firstEmitted);
            firstEmitted = true;
        }

        if (pl.Closed)
            EmitOneSegment(pts[^1], pts[0], radius, worldToLocal, outPrims, matId, isFirstForScene && !firstEmitted);
    }

    private static void EmitOneSegment(
        Vector2 a,
        Vector2 b,
        float radius,
        in Matrix3x3 worldToLocal,
        List<GpuSdfPrim> outPrims,
        int matId,
        bool isFirstForScene)
    {
        var seg = new SdfSegment(a, b);

        outPrims.Add(MakePrim(seg, worldToLocal, matId, OP_UNION, 0f));

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