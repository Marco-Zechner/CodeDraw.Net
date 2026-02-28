using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;

namespace MarcoZechner.CodeDrawDotNet.Drawing.SdfNode.Composition;

public sealed class SdfUnionNode : SdfNodeBase
{
    private ISdf2Node[] _children = [];
    public required ISdf2Node[] Children
    {
        get => _children;
        set { _children = value; MarkDirty(); }
    }

    override internal ISdf2 Build(SdfCompileContext ctx)
    {
        if (_children.Length == 0) return new SdfUnionN([]);

        var compiled = new ISdf2[_children.Length];
        for (var i = 0; i < _children.Length; i++)
            compiled[i] = SdfCompiler.Compile(_children[i], ctx);

        return new SdfUnionN(compiled);
    }
}