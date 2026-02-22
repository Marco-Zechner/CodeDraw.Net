using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Composition;

public sealed class SdfSmoothIntersectNode : SdfNodeBase
{
    public ISdf2Node[] Children = [];
    public float K = 8f;

    override internal ISdf2 Build(SdfCompileContext ctx)
    {
        if (Children.Length == 0) return new SdfSmoothIntersectN([], K);
        var compiled = new ISdf2[Children.Length];
        for (var i = 0; i < Children.Length; i++)
            compiled[i] = SdfCompiler.Compile(Children[i], ctx);
        return new SdfSmoothIntersectN(compiled, K);
    }
}