using MarcoZechner.CodeDrawDotNet.Drawing.Sdf;
using MarcoZechner.CodeDrawDotNet.Drawing.SdfNode;

namespace MarcoZechner.CodeDrawDotNet.Drawing;

internal sealed class SdfMaterialOverrideNode : SdfNodeBase
{
    private ISdf2Node _child = null!;
    public required ISdf2Node Child
    {
        get => _child;
        set { _child = value ?? throw new ArgumentNullException(nameof(value)); MarkDirty(); }
    }

    override internal ISdf2 Build(SdfCompileContext ctx)
        => SdfCompiler.Compile(Child, ctx);
}