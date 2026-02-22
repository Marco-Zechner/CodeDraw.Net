using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Composition;

public sealed class SdfUnionNode : SdfNodeBase
{
    public ISdf2Node[] Children = [];

    override internal ISdf2 Build(SdfCompileContext ctx)
    {
        if (Children.Length == 0) return new SdfUnionN([]);
        var compiled = new ISdf2[Children.Length];
        for (var i = 0; i < Children.Length; i++)
            compiled[i] = SdfCompiler.Compile(Children[i], ctx);
        return new SdfUnionN(compiled);
    }
}