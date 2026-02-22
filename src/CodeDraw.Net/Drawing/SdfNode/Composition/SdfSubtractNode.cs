using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Composition;

public sealed class SdfSubtractNode : SdfNodeBase
{
    public ISdf2Node A = null!;
    public ISdf2Node[] Bs = [];

    override internal ISdf2 Build(SdfCompileContext ctx)
    {
        if (A == null) throw new InvalidOperationException("SdfSubtractNode.A must not be null.");
        var a = SdfCompiler.Compile(A, ctx);

        if (Bs.Length == 0) return a; // nothing to subtract

        var bs = new ISdf2[Bs.Length];
        for (var i = 0; i < Bs.Length; i++)
            bs[i] = SdfCompiler.Compile(Bs[i], ctx);

        return new SdfSubtractN(a, bs);
    }
}