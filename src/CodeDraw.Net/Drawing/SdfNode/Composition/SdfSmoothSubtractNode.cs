using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Composition;

public sealed class SdfSmoothSubtractNode : SdfNodeBase
{
    public ISdf2Node A = null!;
    public ISdf2Node[] Bs = [];
    public float K = 8f;

    override internal ISdf2 Build(SdfCompileContext ctx)
    {
        if (A == null) throw new InvalidOperationException("SdfSmoothSubtractNode.A must not be null.");
        var a = SdfCompiler.Compile(A, ctx);

        if (Bs.Length == 0) return a;

        var bs = new ISdf2[Bs.Length];
        for (var i = 0; i < Bs.Length; i++)
            bs[i] = SdfCompiler.Compile(Bs[i], ctx);

        return new SdfSmoothSubtractN(a, bs, K);
    }
}